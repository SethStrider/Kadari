using Kadari.Application.Abstractions.Persistence;

namespace Kadari.Application.Common;

/// <summary>
/// Default <see cref="IUnitOfWork"/> registered by <c>AddApplication</c>: runs the operation inline with
/// no transaction (handlers commit their own <c>SaveChanges</c>, as before). The real transactional
/// implementation is registered by Infrastructure and wins in the composed app; this keeps the Application
/// layer self-contained and unit-testable without EF.
/// </summary>
internal sealed class PassThroughUnitOfWork : IUnitOfWork
{
    public Task ExecuteInTransactionAsync(Func<CancellationToken, Task> operation, CancellationToken cancellationToken = default)
        => operation(cancellationToken);
}
