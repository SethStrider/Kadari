using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Kadari.Application.Abstractions.Intelligence;
using Kadari.Infrastructure.Enrichment;

namespace Kadari.Infrastructure.Intelligence;

/// <summary>
/// What every hosted-model transport shares: the process-wide concurrency gate, the timeout, and the rule that a
/// failure is a reply (<see cref="ModelReply.Answered"/> false), never an exception — the caller's work goes on
/// without AI. A provider supplies only <see cref="SendAsync"/>.
/// </summary>
public abstract class ModelGatewayBase(VisionConcurrencyGate gate, IOptions<VisionEnricherOptions> options, ILogger logger)
    : IModelGateway
{
    public abstract bool IsConfigured { get; }

    /// <summary>A hosted provider processes data off-shore; a purely local deployment is the operator's call to
    /// reconfigure, so the safe default is true.</summary>
    public virtual bool IsCrossBorder => true;

    protected abstract string Provider { get; }

    protected abstract string Model { get; }

    public async Task<ModelReply> CompleteAsync(ModelCall call, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(call);

        if (call.Attachment is { } attachment && !Accepts(attachment.ContentType))
        {
            return new ModelReply(false, null, $"{Provider} does not accept {attachment.ContentType} attachments.", null);
        }

        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(options.Value.TimeoutSeconds));

        try
        {
            await gate.Semaphore.WaitAsync(timeout.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            logger.LogWarning("Model call for {Feature} timed out waiting for a slot", call.Feature);
            return new ModelReply(false, null, "The AI provider is busy.", null);
        }

        var unknownUsage = new ModelCallUsage(Provider, Model, null, null);
        try
        {
            var (text, usage) = await SendAsync(call, timeout.Token).ConfigureAwait(false);
            return string.IsNullOrWhiteSpace(text)
                ? new ModelReply(false, null, "The AI provider replied with nothing usable.", usage ?? unknownUsage)
                : new ModelReply(true, text, null, usage ?? unknownUsage);
        }
        catch (Exception exception) when (exception is not OperationCanceledException || !cancellationToken.IsCancellationRequested)
        {
            logger.LogWarning(exception, "Model call for {Feature} failed", call.Feature);
            return new ModelReply(false, null, "The AI provider did not answer.", unknownUsage);
        }
        finally
        {
            gate.Semaphore.Release();
        }
    }

    /// <summary>Whether the provider takes this attachment type. Images by default.</summary>
    protected virtual bool Accepts(string contentType) => contentType.StartsWith("image/", StringComparison.OrdinalIgnoreCase);

    /// <summary>Make the provider's call; return the reply text and the usage it reported.</summary>
    protected abstract Task<(string? Text, ModelCallUsage? Usage)> SendAsync(ModelCall call, CancellationToken cancellationToken);
}

/// <summary>No AI provider configured: every call is "not configured", and nothing leaves the process.</summary>
public sealed class UnconfiguredModelGateway : IModelGateway
{
    public bool IsConfigured => false;

    public bool IsCrossBorder => false;

    public Task<ModelReply> CompleteAsync(ModelCall call, CancellationToken cancellationToken) =>
        Task.FromResult(ModelReply.NotConfigured);
}
