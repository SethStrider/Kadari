using Microsoft.EntityFrameworkCore;
using Kadari.Application.Abstractions.Intelligence;
using Kadari.Application.Abstractions.Persistence;
using Kadari.Application.Features.AnalyzeSnap;
using Kadari.Domain.Assets;
using Kadari.Domain.Customers;

namespace Kadari.Application.Features.Intelligence.Services;

/// <summary>Why a model call was not made, when it wasn't.</summary>
public enum ModelSkipReason
{
    None,
    NotConfigured,
    NoCrossBorderConsent,
    QuotaReached,
}

/// <summary>A model reply, or why no call was made.</summary>
public sealed record MeteredReply(ModelReply Reply, ModelSkipReason Skipped)
{
    public bool Answered => Skipped == ModelSkipReason.None && Reply.Answered;
}

/// <summary>
/// The single way Application code calls an AI model (Ripple reference §8). In order: refuse when no provider is
/// configured; refuse to send a customer's data off-shore without their cross-border-AI consent (D8/POPIA); refuse past
/// the daily quota (AS-09); make the call — at most once per request for the same <c>onceKey</c>, so a transient
/// database retry does not bill twice; record usage and cost through <see cref="ModelUsageRecorder"/>. A failure or a
/// refusal is a <see cref="MeteredReply"/>, never an exception: AI is an aid, and the customer's work goes on without it.
/// </summary>
internal sealed class MeteredModel(
    IModelGateway gateway, IKadariDbContext db, VisionQuota quota, ModelUsageRecorder usage)
{
    private readonly Dictionary<object, ModelReply> _made = new(ReferenceEqualityComparer.Instance);

    public async Task<MeteredReply> CompleteAsync(object onceKey, ModelCall call, CustomerId customerId, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(onceKey);
        ArgumentNullException.ThrowIfNull(call);

        if (!gateway.IsConfigured)
        {
            return new MeteredReply(ModelReply.NotConfigured, ModelSkipReason.NotConfigured);
        }

        if (gateway.IsCrossBorder && !await HasCrossBorderConsentAsync(customerId, ct))
        {
            return new MeteredReply(new ModelReply(false, null, "No consent to send data to an off-shore AI.", null),
                ModelSkipReason.NoCrossBorderConsent);
        }

        if (!_made.TryGetValue(onceKey, out var reply))
        {
            if (!await quota.HasRoomAsync(customerId, ct))
            {
                return new MeteredReply(new ModelReply(false, null, "The daily AI allowance is used up.", null),
                    ModelSkipReason.QuotaReached);
            }

            reply = await gateway.CompleteAsync(call, ct);
            _made[onceKey] = reply;
        }

        if (reply.Usage is { } used)
        {
            usage.Record(call.Feature, used, customerId, reply.Answered);
            await quota.RecordCallAsync(customerId, ct);
        }

        return new MeteredReply(reply, ModelSkipReason.None);
    }

    private Task<bool> HasCrossBorderConsentAsync(CustomerId customerId, CancellationToken ct) =>
        db.KadariConsents.AsNoTracking().AnyAsync(
            c => c.CustomerId == customerId && c.Scope == KadariConsentScope.CrossBorderAiEnrichment
                 && c.Status == KadariConsentStatus.Granted, ct);
}
