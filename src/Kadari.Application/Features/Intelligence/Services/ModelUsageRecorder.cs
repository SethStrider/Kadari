using Microsoft.Extensions.Options;
using Kadari.Application.Abstractions.Intelligence;
using Kadari.Application.Abstractions.Persistence;
using Kadari.Domain.Customers;
using Kadari.Domain.Intelligence;

namespace Kadari.Application.Features.Intelligence.Services;

/// <summary>
/// The one writer of <see cref="ModelUsage"/> rows. Prices the call from <see cref="ModelPricingOptions"/> and adds the
/// row to the caller's unit of work, so it commits with the work the call served (and is written again if a
/// transient failure re-runs that work). Used by <see cref="MeteredModel"/> and by capture, whose enricher reports its
/// usage back.
/// </summary>
internal sealed class ModelUsageRecorder(IKadariDbContext db, IOptions<ModelPricingOptions> pricing, TimeProvider clock)
{
    public ModelUsage Record(string feature, ModelCallUsage usage, CustomerId? customerId, bool succeeded)
    {
        ArgumentNullException.ThrowIfNull(usage);
        var options = pricing.Value;
        long? cost = options.Models.TryGetValue(usage.Model, out var price) && (usage.InputTokens is not null || usage.OutputTokens is not null)
            ? (long)Math.Round(
                (usage.InputTokens ?? 0) * price.InputPerMillionTokens + (usage.OutputTokens ?? 0) * price.OutputPerMillionTokens,
                MidpointRounding.AwayFromZero)
            : null;

        var row = ModelUsage.Record(
            feature, usage.Provider, usage.Model, customerId, succeeded, usage.InputTokens, usage.OutputTokens,
            cost, options.Currency, clock.GetUtcNow());
        db.Set<ModelUsage>().Add(row);
        return row;
    }
}
