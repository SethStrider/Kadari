using Kadari.Domain.Assets;
using Kadari.Domain.Subjects;

namespace Kadari.Application.Features.GetTenantPortfolio;

/// <summary>
/// Pure projection of a tenant's whole asset cohort into the insurer portfolio read model — the cross-customer
/// view an insurer console leads with (deck §27–28): how many assets and policyholders, the recorded
/// replacement exposure, how well-evidenced the book is (assurance distribution), and where the value sits
/// (per-category). Kept free of EF/I/O so it is unit-testable with in-memory aggregates; the handler loads the
/// tenant's assets and hands them here.
/// </summary>
public static class TenantPortfolioProjection
{
    public static TenantPortfolioDto From(Guid tenantId, IEnumerable<Asset> assets)
    {
        ArgumentNullException.ThrowIfNull(assets);

        var accumulator = new TenantPortfolioAccumulator(tenantId);
        foreach (var asset in assets)
        {
            accumulator.Add(asset);
        }

        return accumulator.Build();
    }
}

/// <summary>
/// The portfolio as a single pass over the cohort (GL-37), so the handler can stream a tenant's assets instead of
/// holding the whole book — every asset and all its evidence — in memory at once. State is per category, per
/// currency and per policyholder, not per asset.
/// </summary>
/// <remarks>
/// Semantics match the original list-based projection: the portfolio currency is that of the first valued asset (in
/// cohort order, defaulting to ZAR), and exposure counts only values in that currency. Totals are kept per currency
/// until <see cref="Build"/> knows which one applies.
/// </remarks>
public sealed class TenantPortfolioAccumulator(Guid tenantId)
{
    private const string DefaultCurrency = "ZAR";

    private readonly HashSet<SubjectId> _policyholders = [];
    private readonly Dictionary<AssetAssuranceLevel, int> _assurance = [];
    private readonly Dictionary<AssetCategory, int> _categoryCounts = [];
    private readonly Dictionary<(AssetCategory Category, string Currency), long> _categoryExposure = [];
    private readonly Dictionary<string, long> _exposure = [];
    private string? _currency;
    private int _assets;
    private int _valued;

    public void Add(Asset asset)
    {
        ArgumentNullException.ThrowIfNull(asset);

        _assets++;
        _policyholders.Add(asset.OwnerId);
        _assurance[asset.Assurance] = _assurance.GetValueOrDefault(asset.Assurance) + 1;
        _categoryCounts[asset.Category] = _categoryCounts.GetValueOrDefault(asset.Category) + 1;

        if (asset.BestReplacementValue is { } value)
        {
            _valued++;
            _currency ??= value.Currency;
            _exposure[value.Currency] = _exposure.GetValueOrDefault(value.Currency) + value.AmountMinor;
            var key = (asset.Category, value.Currency);
            _categoryExposure[key] = _categoryExposure.GetValueOrDefault(key) + value.AmountMinor;
        }
    }

    public TenantPortfolioDto Build()
    {
        var currency = _currency ?? DefaultCurrency;
        var completeness = _assets == 0 ? 0 : (int)Math.Round(100.0 * _valued / _assets);

        var assurance = Enum.GetValues<AssetAssuranceLevel>()
            .Select(level => new PortfolioAssuranceBandDto(level.ToString(), _assurance.GetValueOrDefault(level)))
            .ToList();

        var categories = _categoryCounts
            .Select(c => new PortfolioCategoryDto(
                c.Key.ToString(), c.Value, _categoryExposure.GetValueOrDefault((c.Key, currency))))
            .OrderByDescending(c => c.ExposureMinor)
            .ThenBy(c => c.Category, StringComparer.OrdinalIgnoreCase)
            .ToList();

        return new TenantPortfolioDto(
            tenantId, _assets, _policyholders.Count, _exposure.GetValueOrDefault(currency), currency, completeness,
            assurance, categories);
    }
}

public sealed record TenantPortfolioDto(
    Guid TenantId,
    int AssetCount,
    int PolicyholderCount,
    long RecordedReplacementExposureMinor,
    string Currency,
    int CompletenessPercent,
    IReadOnlyList<PortfolioAssuranceBandDto> AssuranceBreakdown,
    IReadOnlyList<PortfolioCategoryDto> Categories);

public sealed record PortfolioAssuranceBandDto(string Assurance, int Count);

public sealed record PortfolioCategoryDto(string Category, int Count, long ExposureMinor);
