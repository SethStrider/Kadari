using Microsoft.EntityFrameworkCore;
using Npgsql;
using Testcontainers.PostgreSql;
using Kadari.Infrastructure.Persistence;

namespace Kadari.Infrastructure.Tests;

/// <summary>
/// One Postgres container shared by the whole Infrastructure test suite. A migrated <em>template</em>
/// database is built once at start-up; every test then gets its own fast clone of it
/// (<c>CREATE DATABASE … TEMPLATE …</c>). That preserves per-test isolation — each test still runs on a
/// pristine database — without paying container-startup cost 30+ times (the painful part on Docker
/// Desktop). Real Postgres, like-for-like with production.
/// </summary>
public sealed class PostgresFixture : IAsyncLifetime
{
    private const string TemplateDatabase = "trustli_template";

    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder()
        .WithImage("postgres:17")
        // Each test acquires its own uniquely-named cloned database, and Npgsql keeps a separate pool
        // per connection string; even with per-test pool cleanup, headroom above the default 100 keeps
        // the growing suite comfortably clear of "53300: too many clients already".
        .WithCommand("-c", "max_connections=500")
        .Build();

    private string _adminConnectionString = null!;

    public async Task InitializeAsync()
    {
        await _postgres.StartAsync();
        _adminConnectionString = _postgres.GetConnectionString();

        // Build the migrated template once. Pooling=false so no idle connection lingers on the
        // template — CREATE DATABASE … TEMPLATE requires the source to have no active connections.
        await ExecuteAsync(_adminConnectionString, $"CREATE DATABASE \"{TemplateDatabase}\"");

        var options = new DbContextOptionsBuilder<KadariDbContext>()
            .UseNpgsql(ConnectionStringFor(TemplateDatabase, pooling: false))
            .Options;
        await using var db = new KadariDbContext(options);
        await db.Database.MigrateAsync();
    }

    public Task DisposeAsync() => _postgres.DisposeAsync().AsTask();

    /// <summary>A fresh, already-migrated database (a clone of the template). For the KadariDbContext tests.</summary>
    public async Task<string> CreateDatabaseAsync()
    {
        var name = UniqueName();
        await ExecuteAsync(_adminConnectionString, $"CREATE DATABASE \"{name}\" TEMPLATE \"{TemplateDatabase}\"");
        return ConnectionStringFor(name);
    }

    /// <summary>A fresh, empty database (no schema). For tests that call EnsureCreated on their own model.</summary>
    public async Task<string> CreateEmptyDatabaseAsync()
    {
        var name = UniqueName();
        await ExecuteAsync(_adminConnectionString, $"CREATE DATABASE \"{name}\"");
        return ConnectionStringFor(name);
    }

    /// <summary>
    /// Releases a per-test database: clears the pool Npgsql opened for its (unique) connection string so
    /// no idle connection lingers, then drops the database. Called from <see cref="InfraTest.DisposeAsync"/>
    /// so pools/connections don't accumulate across the suite and exhaust the server's client limit.
    /// </summary>
    public async Task ReleaseDatabaseAsync(string connectionString)
    {
        NpgsqlConnection.ClearPool(new NpgsqlConnection(connectionString));

        var database = new NpgsqlConnectionStringBuilder(connectionString).Database!;
        await ExecuteAsync(
            _adminConnectionString,
            $"DROP DATABASE IF EXISTS \"{database}\" WITH (FORCE)");
    }

    private static string UniqueName() => $"t_{Guid.NewGuid():N}";

    private string ConnectionStringFor(string database, bool pooling = true) =>
        new NpgsqlConnectionStringBuilder(_adminConnectionString)
        {
            Database = database,
            Pooling = pooling,
        }.ConnectionString;

    private static async Task ExecuteAsync(string connectionString, string sql)
    {
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        await command.ExecuteNonQueryAsync();
    }
}

/// <summary>
/// Puts every Infrastructure test in one xUnit collection so they share a single
/// <see cref="PostgresFixture"/> (one container for the whole assembly).
/// </summary>
[CollectionDefinition(Name)]
public sealed class PostgresCollection : ICollectionFixture<PostgresFixture>
{
    public const string Name = "postgres";
}

/// <summary>
/// Base for Infrastructure tests: acquires a fresh cloned database per test from the shared fixture and
/// exposes its <see cref="ConnectionString"/>. Derived classes just build their context against it.
/// Each concrete test class carries <c>[Collection(PostgresCollection.Name)]</c> so xUnit injects the
/// shared fixture.
/// </summary>
public abstract class InfraTest(PostgresFixture fixture) : IAsyncLifetime
{
    protected PostgresFixture Fixture { get; } = fixture;

    protected string ConnectionString { get; private set; } = null!;

    public virtual async Task InitializeAsync() => ConnectionString = await AcquireDatabaseAsync();

    public virtual async Task DisposeAsync()
    {
        if (ConnectionString is not null)
        {
            await Fixture.ReleaseDatabaseAsync(ConnectionString);
        }
    }

    /// <summary>The database for this test — a migrated clone of the template by default. Override to
    /// get an empty database (e.g. tests that call EnsureCreated on their own model).</summary>
    protected virtual Task<string> AcquireDatabaseAsync() => Fixture.CreateDatabaseAsync();
}
