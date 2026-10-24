using Kadari.Application.Abstractions.Intelligence;

namespace Kadari.Application.Common;

/// <summary>
/// Default <see cref="IModelGateway"/> registered by <c>AddApplication</c>: no provider. Keeps the Application layer
/// runnable without Infrastructure (unit tests, in-memory hosts); Infrastructure registers the configured provider.
/// </summary>
internal sealed class NoModelGateway : IModelGateway
{
    public bool IsConfigured => false;

    public bool IsCrossBorder => false;

    public Task<ModelReply> CompleteAsync(ModelCall call, CancellationToken cancellationToken) =>
        Task.FromResult(ModelReply.NotConfigured);
}
