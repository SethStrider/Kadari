using Kadari.Application.Abstractions.Persistence;

namespace Kadari.Application.Behaviors;

/// <summary>
/// The scoped queue behind <see cref="IPostCommitActions"/>, driven by <see cref="TransactionalDispatchBehavior{TRequest,TResponse}"/>:
/// it tracks command nesting so only the outermost command runs the queue, and is cleared at the start of every
/// transaction attempt so a retried command does not queue its actions twice.
/// </summary>
public sealed class PostCommitActions : IPostCommitActions
{
    private readonly List<Func<CancellationToken, Task>> _actions = [];
    private int _depth;

    public void Enqueue(Func<CancellationToken, Task> action)
    {
        ArgumentNullException.ThrowIfNull(action);
        _actions.Add(action);
    }

    /// <summary>Enters a command; true when it is the outermost one.</summary>
    public bool Enter() => _depth++ == 0;

    public void Exit() => _depth--;

    /// <summary>Drops everything queued (a new transaction attempt, or a failed command).</summary>
    public void Reset() => _actions.Clear();

    /// <summary>Runs and clears the queue, in the order the actions were queued.</summary>
    public async Task RunAsync(CancellationToken ct)
    {
        var actions = _actions.ToArray();
        _actions.Clear();
        foreach (var action in actions)
        {
            await action(ct);
        }
    }
}
