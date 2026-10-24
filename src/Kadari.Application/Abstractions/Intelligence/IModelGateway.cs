namespace Kadari.Application.Abstractions.Intelligence;

/// <summary>An image or document sent with a model call.</summary>
public sealed record ModelAttachment(string ContentType, byte[] Content);

/// <summary>One model call: what it is for (the usage row's feature), the instructions, and an optional attachment.
/// The reply is asked for as JSON.</summary>
public sealed record ModelCall(string Feature, string SystemPrompt, string UserPrompt, ModelAttachment? Attachment);

/// <summary>What the provider reports a call used.</summary>
public sealed record ModelCallUsage(string Provider, string Model, int? InputTokens, int? OutputTokens);

/// <summary>
/// The model's answer. <see cref="Answered"/> is false when the provider failed, refused the attachment or replied
/// with nothing — the caller then falls back, it never fails the customer's work. <see cref="Usage"/> is present
/// whenever a provider was actually called, answered or not.
/// </summary>
public sealed record ModelReply(bool Answered, string? Text, string? FailureReason, ModelCallUsage? Usage)
{
    public static ModelReply NotConfigured { get; } = new(false, null, "No AI provider is configured.", null);
}

/// <summary>
/// The transport to the configured AI provider (Gemini, an OpenAI-compatible endpoint, or none). Implemented in
/// Infrastructure. <b>Never inject this directly</b>: every call goes through <c>MeteredModel</c>, the one writer of
/// usage and cost, which also applies the consent gate and the daily quota (enforced by an architecture test).
/// </summary>
public interface IModelGateway
{
    /// <summary>Whether a real provider is configured.</summary>
    bool IsConfigured { get; }

    /// <summary>Whether the provider processes data outside South Africa (POPIA cross-border disclosure, D8).</summary>
    bool IsCrossBorder { get; }

    Task<ModelReply> CompleteAsync(ModelCall call, CancellationToken cancellationToken);
}
