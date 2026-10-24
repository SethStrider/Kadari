using Kadari.Application.Abstractions.Intelligence;
using Kadari.Application.Features.Intelligence.Services;
using Kadari.Domain.Customers;

namespace Kadari.Application.Features.Assets.Services;

/// <summary>
/// Sends one receipt or label to the model through <see cref="MeteredModel"/> and turns the reply into a
/// <see cref="PurchaseProposalDto"/>. Shared by the two reading slices; it saves nothing but the usage row — the owner
/// confirms, and only then are facts recorded (CARE/12 §9, CARE/06 §4).
/// </summary>
internal sealed class PurchaseReader(MeteredModel model, TimeProvider clock)
{
    public async Task<PurchaseProposalDto> ReadAsync(
        object onceKey, string feature, string prompt, ModelAttachment document, Guid customerId, CancellationToken ct)
    {
        var reply = await model.CompleteAsync(
            onceKey, new ModelCall(feature, PurchaseExtraction.System, prompt, document), new CustomerId(customerId), ct);

        return reply.Skipped switch
        {
            ModelSkipReason.NotConfigured => PurchaseProposalDto.Nothing(ExtractionUnavailable.NotConfigured),
            ModelSkipReason.NoCrossBorderConsent => PurchaseProposalDto.Nothing(ExtractionUnavailable.NoCrossBorderConsent),
            ModelSkipReason.QuotaReached => PurchaseProposalDto.Nothing(ExtractionUnavailable.QuotaReached),
            _ when !reply.Reply.Answered && reply.Reply.Usage is null => PurchaseProposalDto.Nothing(ExtractionUnavailable.UnsupportedDocument),
            _ when !reply.Reply.Answered => PurchaseProposalDto.Nothing(ExtractionUnavailable.NothingRead),
            _ => PurchaseExtraction.Parse(reply.Reply.Text, DateOnly.FromDateTime(clock.GetUtcNow().UtcDateTime)),
        };
    }
}
