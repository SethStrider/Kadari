using Microsoft.EntityFrameworkCore;
using Kadari.Application.Abstractions.Messaging;
using Kadari.Application.Abstractions.Persistence;
using Kadari.Application.Exceptions;

namespace Kadari.Infrastructure.Persistence;

/// <summary>
/// Real transactional <see cref="IUnitOfWork"/> over <see cref="KadariDbContext"/>. Runs the operation in
/// one transaction — honouring the Npgsql retrying execution strategy — committing on success and rolling
/// back on failure, so the command's writes and the outbox rows for its integration events are atomic.
/// An <see cref="IPersistableRejection"/> commits the work done so far, then rethrows (e.g. a failed OTP
/// attempt). A nested call (a transaction is already open) joins it — the outermost owns commit/rollback.
/// <para>
/// EF's <see cref="DbUpdateConcurrencyException"/> (an <c>xmin</c> mismatch) is translated here into an
/// application <see cref="ConcurrencyException"/>: this is the boundary that owns EF, so the layers above
/// — and the API edge that maps it to 409 — never have to know about the persistence technology.
/// </para>
/// </summary>
public sealed class UnitOfWork(KadariDbContext db) : IUnitOfWork
{
    public async Task ExecuteInTransactionAsync(
        Func<CancellationToken, Task> operation, CancellationToken cancellationToken = default)
    {
        try
        {
            await RunAsync(operation, cancellationToken);
        }
        catch (DbUpdateConcurrencyException ex)
        {
            throw new ConcurrencyException(
                "The resource was modified by another request. Re-read it and retry.", ex);
        }
    }

    private async Task RunAsync(Func<CancellationToken, Task> operation, CancellationToken cancellationToken)
    {
        // Nested Send inside an active transaction — participate, don't nest.
        if (db.Database.CurrentTransaction is not null)
        {
            await operation(cancellationToken);
            return;
        }

        // The execution strategy re-runs the whole delegate on a transient failure, beginning a fresh
        // transaction each attempt — the correct pattern for user-initiated transactions under retry.
        var strategy = db.Database.CreateExecutionStrategy();
        var attempt = 0;
        await strategy.ExecuteAsync(async () =>
        {
            // A retry must start from nothing. The failed attempt's rolled-back inserts are still tracked as
            // Added, so re-running the command on top of them would write every row twice — two copies of each
            // captured asset, a settled payment's cascade doubled (GL-35). Everything the command needs is loaded
            // inside this delegate, so clearing loses nothing.
            if (++attempt > 1)
            {
                db.ChangeTracker.Clear();
            }

            await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
            try
            {
                await operation(cancellationToken);
                await transaction.CommitAsync(cancellationToken);
            }
            catch (Exception ex) when (ex is IPersistableRejection)
            {
                // Persist the side-effect committed so far, then surface the rejection.
                await transaction.CommitAsync(cancellationToken);
                throw;
            }
            // Any other exception: the transaction disposes without committing → rollback, then propagates.
        });
    }
}
