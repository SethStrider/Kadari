namespace Kadari.Application.Abstractions.Persistence;

/// <summary>
/// Runs a unit of work inside a single database transaction so the command's writes and the
/// <c>OutboxMessage</c> rows for any integration events it raises commit together — both or neither.
/// Keeps EF/transaction control out of the Application layer. Implementations must:
/// <list type="bullet">
///   <item>join an already-active transaction (nested <c>Send</c>) instead of nesting;</item>
///   <item>commit on success, roll back on failure;</item>
///   <item>on an <see cref="Messaging.IPersistableRejection"/>, commit the work done so far, then
///   rethrow — so a deliberate rejection can still persist its side-effect (e.g. a failed OTP attempt).</item>
/// </list>
/// </summary>
public interface IUnitOfWork
{
    Task ExecuteInTransactionAsync(Func<CancellationToken, Task> operation, CancellationToken cancellationToken = default);
}
