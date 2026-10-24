using Kadari.Domain.Customers;

namespace Kadari.Domain.Intelligence;

public readonly record struct ModelUsageId(Guid Value)
{
    public static ModelUsageId New() => new(Guid.CreateVersion7());
}

/// <summary>
/// One call to an AI model — what it was for, which provider and model answered, the tokens it used and what that
/// cost (Ripple reference §8, CARE/16 non-negotiables: "all model calls go through one abstraction that records usage
/// and cost"). Append-only, like <c>AuditEvent</c>: written in the same transaction as the work that made the call,
/// never updated, never soft-deleted. A failed call is still recorded, because it can still be billed.
/// </summary>
/// <remarks>Cost is in <b>micro-units</b> (millionths) of <see cref="Currency"/>: a single call costs fractions of a
/// cent, which minor units cannot hold. Null when the model has no configured price.</remarks>
public sealed class ModelUsage
{
    private ModelUsage() { }   // EF

    public ModelUsageId Id { get; private init; }
    public string Feature { get; private init; } = null!;
    public string Provider { get; private init; } = null!;
    public string Model { get; private init; } = null!;
    public CustomerId? CustomerId { get; private init; }
    public bool Succeeded { get; private init; }
    public int? InputTokens { get; private init; }
    public int? OutputTokens { get; private init; }
    public long? CostMicros { get; private init; }
    public string? Currency { get; private init; }
    public DateTimeOffset OccurredAt { get; private init; }

    public static ModelUsage Record(
        string feature, string provider, string model, CustomerId? customerId, bool succeeded,
        int? inputTokens, int? outputTokens, long? costMicros, string? currency, DateTimeOffset occurredAt)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(feature);
        ArgumentException.ThrowIfNullOrWhiteSpace(provider);
        ArgumentException.ThrowIfNullOrWhiteSpace(model);
        if (inputTokens < 0 || outputTokens < 0 || costMicros < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(inputTokens), "Usage cannot be negative.");
        }

        return new ModelUsage
        {
            Id = ModelUsageId.New(),
            Feature = feature.Trim(),
            Provider = provider.Trim(),
            Model = model.Trim(),
            CustomerId = customerId,
            Succeeded = succeeded,
            InputTokens = inputTokens,
            OutputTokens = outputTokens,
            CostMicros = costMicros,
            Currency = costMicros is null ? null : (currency ?? "USD").Trim().ToUpperInvariant(),
            OccurredAt = occurredAt,
        };
    }
}
