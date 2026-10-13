using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Kadari.Infrastructure.Persistence;

/// <summary>
/// Design-time factory used by <c>dotnet ef</c> to build the context for scaffolding migrations.
/// At run time the context is configured by Aspire; this only supplies a provider + placeholder
/// connection so the model can be evaluated offline (no database is contacted for `migrations add`).
/// </summary>
public sealed class KadariDbContextFactory : IDesignTimeDbContextFactory<KadariDbContext>
{
    public KadariDbContext CreateDbContext(string[] args)
    {
        var options = new DbContextOptionsBuilder<KadariDbContext>()
            .UseNpgsql("Host=localhost;Database=kadari;Username=postgres;Password=postgres")
            .Options;

        return new KadariDbContext(options);
    }
}
