using Kadari.Application.Features.CareRegistry.Services;

namespace Kadari.Infrastructure.CareRegistry;

/// <summary>Where the registry's files are and what to stamp on them, bound from <c>CareRegistry</c>.</summary>
public sealed class RegistryFilesOptions
{
    public const string SectionName = "CareRegistry";

    /// <summary>The founder's research workbook extracts (<c>docs/authority/CARE/REGISTRY</c>). Default: the copy
    /// shipped beside the migration runner.</summary>
    public string ResearchDirectory { get; set; } = Path.Combine(AppContext.BaseDirectory, "registry", "research");

    /// <summary>KADARI's own researched coverage rules (<c>docs/care/registry/coverage-rules.csv</c>).</summary>
    public string CoverageRulesFile { get; set; } = Path.Combine(AppContext.BaseDirectory, "registry", "coverage-rules.csv");

    /// <summary>KADARI's own researched registration rules (<c>docs/care/registry/registration-rules.csv</c>).</summary>
    public string RegistrationRulesFile { get; set; } = Path.Combine(AppContext.BaseDirectory, "registry", "registration-rules.csv");

    /// <summary>KADARI's own researched routing rules (<c>docs/care/registry/routing-rules.csv</c>).</summary>
    public string RoutingRulesFile { get; set; } = Path.Combine(AppContext.BaseDirectory, "registry", "routing-rules.csv");

    /// <summary>When the workbook research was done (CARE/16 is dated 2026-10-06). The workbook gives no per-row
    /// retrieval date, so this is what the sources record.</summary>
    public DateTimeOffset ResearchedAt { get; set; } = new(2026, 10, 6, 0, 0, 0, TimeSpan.Zero);

    /// <summary>The country the research covers.</summary>
    public string CountryCode { get; set; } = "ZA";
}

/// <summary>
/// Reads the registry's CSV files into a <see cref="RegistrySnapshot"/>. File I/O only — what the rows mean is the
/// importer's job. The research backlog (05) is a planning document, and the warranty-automation leads (04) are
/// research notes without a mode, destination or deadline; researched registration rules go in
/// <c>registration-rules.csv</c> instead. Neither 04 nor 05 is imported.
/// </summary>
public static class RegistryFiles
{
    public const string Retailers = "01_RETAILER_UNIVERSE.csv";
    public const string Brands = "02_BRAND_REGISTRY.csv";
    public const string RoutingModels = "03_ROUTING_MODELS.csv";

    public static async Task<RegistrySnapshot> LoadAsync(RegistryFilesOptions options, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(options);

        var retailers = await ReadAsync(Path.Combine(options.ResearchDirectory, Retailers), required: true, ct);
        var brands = await ReadAsync(Path.Combine(options.ResearchDirectory, Brands), required: true, ct);
        var routing = await ReadAsync(Path.Combine(options.ResearchDirectory, RoutingModels), required: true, ct);
        var rules = await ReadAsync(options.CoverageRulesFile, required: false, ct);
        var registrations = await ReadAsync(options.RegistrationRulesFile, required: false, ct);
        var routes = await ReadAsync(options.RoutingRulesFile, required: false, ct);

        return new RegistrySnapshot(
            options.ResearchedAt,
            options.CountryCode.Trim().ToUpperInvariant(),
            brands.Select(r => new BrandRow(
                r.Line, r["Brand"] ?? "", r["Global / Parent Group"], r["South African Owner / Distributor / Admin"],
                r["Main Categories"], r["Primary Service Model"], r["Registration / Extension Signal"], r["Priority"],
                r["Research Confidence"], r["Primary Source"])).ToList(),
            retailers.Select(r => new RetailerRow(
                r.Line, r["Retailer / Channel"] ?? "", r["Group / Owner"], r["Retail Format"], r["Main Categories"],
                r["Integration Stance"], r["Priority"], r["Primary Source"])).ToList(),
            routing.Select(r => new RoutingModelRow(
                r.Line, r["Code"] ?? "", r["Model"] ?? "", r["When it Applies"], r["KADARI Default Action"],
                r["Typical Evidence Needed"], r["Examples"])).ToList(),
            rules.Select(r => new CoverageRuleRow(
                r.Line, r["RuleKey"] ?? "", r["Country"], r["Brand"], r["Category"], r["Model"], r["Retailer"],
                r["CoverageType"], r["DurationMonths"], r["StartBasis"], r["RegistrationRequired"],
                r["RegistrationDeadlineDays"], r["ExtensionMonths"], r["Summary"], r["EffectiveFrom"], r["EffectiveTo"],
                r["Confidence"], r["SourceTitle"], r["SourceUrl"], r["LastVerified"], r["Status"])).ToList(),
            registrations.Select(r => new RegistrationRuleRow(
                r.Line, r["RuleKey"] ?? "", r["Country"], r["Brand"], r["Category"], r["Model"], r["Mode"], r["DestinationType"],
                r["Destination"], r["RequiredFields"], r["DeadlineDays"], r["ExtensionMonths"], r["Benefit"], r["Instructions"],
                r["Confidence"], r["SourceTitle"], r["SourceUrl"], r["LastVerified"], r["Status"])).ToList(),
            routes.Select(r => new RoutingRuleRow(
                r.Line, r["RuleKey"] ?? "", r["Country"], r["Brand"], r["Category"], r["Model"], r["Retailer"], r["Coverage"],
                r["IssueClass"], r["RouteType"], r["RoutingModel"], r["Target"], r["ChannelType"], r["Channel"],
                r["RequiredEvidence"], r["Instructions"], r["Priority"], r["Confidence"], r["SourceTitle"], r["SourceUrl"],
                r["LastVerified"], r["Status"])).ToList());
    }

    private static async Task<IReadOnlyList<CsvTable.Row>> ReadAsync(string path, bool required, CancellationToken ct)
    {
        if (!File.Exists(path))
        {
            return required ? throw new FileNotFoundException($"Registry file '{path}' is missing.", path) : [];
        }

        return CsvTable.Parse(await File.ReadAllTextAsync(path, ct));
    }
}
