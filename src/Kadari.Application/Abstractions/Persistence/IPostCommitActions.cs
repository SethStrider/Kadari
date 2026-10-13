namespace Kadari.Application.Abstractions.Persistence;

/// <summary>
/// Work a command handler must do only once its transaction has committed: an external send that must not
/// fire twice when a transient database error makes the unit of work re-run the command (CLAUDE.md §6a).
/// <para>
/// Queued actions run once, after the outermost command commits, and never if it fails or rolls back. They are
/// in-process, not durable: if the process dies between the commit and the action, the action is lost. Use it
/// for work the user can simply ask for again (a sign-in code); use the outbox for anything that must happen.
/// </para>
/// </summary>
public interface IPostCommitActions
{
    void Enqueue(Func<CancellationToken, Task> action);
}
