using MediatR;
using Kadari.Application.Abstractions.Messaging;
using Kadari.Application.Abstractions.Persistence;

namespace Kadari.Application.Behaviors;

/// <summary>
/// Runs every command inside one transaction (via <see cref="IUnitOfWork"/>) and, still inside it,
/// dispatches the domain events raised by aggregates. So the command's writes, the whole synchronous
/// reaction cascade, and any integration events a translator publishes to the outbox commit atomically —
/// both or neither. Queries pass through untouched (no transaction, no dispatch).
/// <para>
/// Domain-event handlers may raise further events (engine chaining, e.g. PaymentSettled → workflow →
/// tasks); the drain loop is bounded by <see cref="MaxCascades"/>. A final <c>SaveChanges</c> flushes any
/// buffered outbox messages into the same transaction before commit.
/// </para>
/// <para>
/// After the outermost command commits, it runs the <see cref="IPostCommitActions"/> the command queued, once.
/// The queue is cleared at the start of every transaction attempt, so a retried command does not queue twice,
/// and is dropped when the command fails.
/// </para>
/// </summary>
public sealed class TransactionalDispatchBehavior<TRequest, TResponse>(
    IKadariDbContext db, IDomainEventDispatcher dispatcher, IUnitOfWork unitOfWork, PostCommitActions postCommit)
    : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
{
    // Cap the cascade so a misbehaving handler can't loop forever.
    private const int MaxCascades = 16;

    public async Task<TResponse> Handle(
        TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken ct)
    {
        // Queries change no state and raise no domain events — never open a write transaction for them.
        if (request is not IBaseCommand)
        {
            return await next(ct);
        }

        TResponse response = default!;
        var outermost = postCommit.Enter();
        try
        {
            await unitOfWork.ExecuteInTransactionAsync(async innerCt =>
            {
                if (outermost)
                {
                    postCommit.Reset();   // a retried attempt starts with an empty queue
                }

                response = await next(innerCt);

                // Drain iteratively inside the transaction. A translator handler publishes an integration
                // event (buffered to the outbox); a cascade handler may raise further domain events.
                for (var cascade = 0; cascade < MaxCascades; cascade++)
                {
                    var domainEvents = db.DequeueDomainEvents();
                    if (domainEvents.Count == 0)
                    {
                        break;
                    }

                    await dispatcher.DispatchAsync(domainEvents, innerCt);
                }

                // Flush buffered outbox messages (and any pending changes) so they land in this transaction.
                await db.SaveChangesAsync(innerCt);
            }, ct);
        }
        catch
        {
            if (outermost)
            {
                postCommit.Reset();   // nothing committed (or a rejection): nothing may follow it
            }

            throw;
        }
        finally
        {
            postCommit.Exit();
        }

        if (outermost)
        {
            await postCommit.RunAsync(ct);
        }

        return response;
    }
}
