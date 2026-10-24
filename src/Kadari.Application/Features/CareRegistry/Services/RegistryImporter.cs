using System.Globalization;
using Microsoft.EntityFrameworkCore;
using Kadari.Application.Abstractions.Persistence;
using Kadari.Application.Common;
using Kadari.Domain.Abstractions;
using Kadari.Domain.CareRegistry;

namespace Kadari.Application.Features.CareRegistry.Services;

/// <summary>
/// Turns a <see cref="RegistrySnapshot"/> into registry rows (CARE/14 C2, CARE/10). One routine both plans and applies,
/// so a preview and the apply that follows it cannot disagree:
/// <list type="bullet">
/// <item>ids are deterministic from the natural key (brand slug, retailer country + slug, routing code, source URL,
/// rule key + version), so re-importing the same files is a no-op;</item>
/// <item>reference rows (sources, brands, retailers, routing models) are added or updated in place — they carry no
/// decision history;</item>
/// <item>a coverage rule is never edited: a changed row becomes the next version of its rule, and publishing it
/// supersedes the live version, which stays addressable for the decisions that cite it;</item>
/// <item>anything unparseable, unknown or unpublishable is an error and blocks the whole import — a half-applied
/// registry is worse than a stale one.</item>
/// </list>
/// </summary>
internal sealed class RegistryImporter(IKadariDbContext db, TimeProvider clock)
{
    private const string IdPrefix = "care-registry:";

    public Task<RegistryImportPlan> PlanAsync(RegistrySnapshot snapshot, CancellationToken ct) => RunAsync(snapshot, apply: false, ct);

    public Task<RegistryImportPlan> ApplyAsync(RegistrySnapshot snapshot, CancellationToken ct) => RunAsync(snapshot, apply: true, ct);

    private async Task<RegistryImportPlan> RunAsync(RegistrySnapshot snapshot, bool apply, CancellationToken ct)
    {
        var run = new Run(snapshot, apply, clock.GetUtcNow());

        IQueryable<T> Load<T>() where T : class => apply ? db.Set<T>() : db.Set<T>().AsNoTracking();

        var sources = await Load<SourceEvidence>().ToDictionaryAsync(s => s.Id, ct);
        var brands = await Load<Brand>().ToDictionaryAsync(b => b.Slug, ct);
        var retailers = await Load<Retailer>().Where(r => r.CountryCode == snapshot.CountryCode).ToDictionaryAsync(r => r.Slug, ct);
        var routing = await Load<RoutingModel>().ToDictionaryAsync(r => r.Code, ct);
        var rules = (await Load<CoverageRuleVersion>().ToListAsync(ct)).ToLookup(r => r.LogicalRuleId);
        var registrationRules = (await Load<RegistrationRuleVersion>().ToListAsync(ct)).ToLookup(r => r.LogicalRuleId);

        ImportBrands(run, snapshot, brands, sources);
        ImportRetailers(run, snapshot, retailers, sources);
        ImportRoutingModels(run, snapshot, routing);
        ImportCoverageRules(run, snapshot, brands, retailers, sources, rules);
        ImportRegistrationRules(run, snapshot, brands, sources, registrationRules);

        return new RegistryImportPlan(run.Changes, run.Errors);
    }

    private void ImportBrands(Run run, RegistrySnapshot snapshot, Dictionary<string, Brand> brands, Dictionary<SourceEvidenceId, SourceEvidence> sources)
    {
        var seen = new HashSet<string>();
        foreach (var row in snapshot.Brands)
        {
            if (!TrySlug(row.Name, out var slug) || !seen.Add(slug))
            {
                run.Error(row.Line, "brands", $"'{row.Name}' is blank or listed twice.");
                continue;
            }

            var source = Source(run, sources, row.SourceUrl, $"{row.Name.Trim()} — Care research registry", SourceType.WebPage, row.Line);
            var confidence = ParseEnum<RegistryConfidence>(row.ResearchConfidence) ?? RegistryConfidence.Low;

            var isNew = !brands.TryGetValue(slug, out var brand);
            brand ??= Brand.Create(new BrandId(DeterministicGuid.From($"{IdPrefix}brand:{slug}")), row.Name);
            var changed = brand.Describe(row.ParentGroup, row.LocalOperator, row.Categories, row.ServiceModel,
                row.RegistrationSignal, row.Priority, confidence, source);

            if (isNew)
            {
                brands[slug] = brand;
                if (run.Apply)
                {
                    db.Set<Brand>().Add(brand);
                }
            }

            run.Change(RegistryEntity.Brand, slug, isNew ? RegistryAction.Add : changed ? RegistryAction.Update : RegistryAction.Unchanged,
                $"{brand.Name}|{brand.Priority}|{brand.ResearchConfidence}");
        }
    }

    private void ImportRetailers(Run run, RegistrySnapshot snapshot, Dictionary<string, Retailer> retailers, Dictionary<SourceEvidenceId, SourceEvidence> sources)
    {
        var seen = new HashSet<string>();
        foreach (var row in snapshot.Retailers)
        {
            if (!TrySlug(row.Name, out var slug) || !seen.Add(slug))
            {
                run.Error(row.Line, "retailers", $"'{row.Name}' is blank or listed twice.");
                continue;
            }

            var source = Source(run, sources, row.SourceUrl, $"{row.Name.Trim()} — Care research registry", SourceType.WebPage, row.Line);

            var isNew = !retailers.TryGetValue(slug, out var retailer);
            retailer ??= Retailer.Create(
                new RetailerId(DeterministicGuid.From($"{IdPrefix}retailer:{snapshot.CountryCode}:{slug}")), row.Name, snapshot.CountryCode);
            var changed = retailer.Describe(row.GroupOwner, row.RetailFormat, row.Categories, row.IntegrationStance, row.Priority, source);

            if (isNew)
            {
                retailers[slug] = retailer;
                if (run.Apply)
                {
                    db.Set<Retailer>().Add(retailer);
                }
            }

            run.Change(RegistryEntity.Retailer, slug, isNew ? RegistryAction.Add : changed ? RegistryAction.Update : RegistryAction.Unchanged,
                $"{retailer.Name}|{retailer.Priority}");
        }
    }

    private void ImportRoutingModels(Run run, RegistrySnapshot snapshot, Dictionary<string, RoutingModel> routing)
    {
        foreach (var row in snapshot.RoutingModels)
        {
            RoutingModel model;
            bool isNew;
            try
            {
                var code = row.Code.Trim().ToUpperInvariant();
                isNew = !routing.TryGetValue(code, out var existing);
                model = existing ?? RoutingModel.Create(new RoutingModelId(DeterministicGuid.From($"{IdPrefix}routing:{code}")), code, row.Name);
            }
            catch (Exception ex) when (ex is DomainException or ArgumentException)
            {
                run.Error(row.Line, "routing models", ex.Message);
                continue;
            }

            var changed = model.Describe(row.Name, row.WhenItApplies, row.DefaultAction, row.TypicalEvidence, row.Examples);
            if (isNew)
            {
                routing[model.Code] = model;
                if (run.Apply)
                {
                    db.Set<RoutingModel>().Add(model);
                }
            }

            run.Change(RegistryEntity.RoutingModel, model.Code,
                isNew ? RegistryAction.Add : changed ? RegistryAction.Update : RegistryAction.Unchanged, model.Name);
        }
    }

    private void ImportCoverageRules(
        Run run, RegistrySnapshot snapshot, Dictionary<string, Brand> brands, Dictionary<string, Retailer> retailers,
        Dictionary<SourceEvidenceId, SourceEvidence> sources, ILookup<Guid, CoverageRuleVersion> existing)
    {
        var seen = new HashSet<string>();
        foreach (var row in snapshot.CoverageRules)
        {
            var key = row.RuleKey?.Trim().ToLowerInvariant() ?? "";
            if (key.Length == 0 || !seen.Add(key))
            {
                run.Error(row.Line, "coverage rules", $"Rule key '{row.RuleKey}' is blank or listed twice.");
                continue;
            }

            if (!TryParseRule(row, snapshot, brands, retailers, out var parsed, out var problem))
            {
                run.Error(row.Line, "coverage rules", problem);
                continue;
            }

            if (string.IsNullOrWhiteSpace(row.SourceTitle))
            {
                run.Error(row.Line, "coverage rules", "Every rule needs a source title.");
                continue;
            }

            var source = Source(run, sources, row.SourceUrl, row.SourceTitle,
                string.IsNullOrWhiteSpace(row.SourceUrl) ? SourceType.Document : SourceType.WebPage, row.Line, row.SourceTitle);
            if (source is null)
            {
                continue;
            }

            var logical = DeterministicGuid.From($"{IdPrefix}coverage-rule:{key}");
            var versions = existing[logical].OrderBy(v => v.Version).ToList();
            var latest = versions.LastOrDefault();
            var next = (latest?.Version ?? 0) + 1;

            CoverageRuleVersion candidate;
            try
            {
                candidate = CoverageRuleVersion.Draft(
                    new CoverageRuleVersionId(DeterministicGuid.From($"{IdPrefix}coverage-rule:{key}:v{next}")), logical, next,
                    parsed.Country, parsed.Brand?.Id, parsed.Category, row.Model, parsed.Retailer?.Id, parsed.Type, parsed.Months,
                    parsed.StartBasis, parsed.RegistrationRequired, parsed.DeadlineDays, parsed.ExtensionMonths, row.Summary ?? "",
                    parsed.EffectiveFrom, parsed.EffectiveTo, parsed.Confidence, source.Value, parsed.LastVerified,
                    latest?.Id);
            }
            catch (Exception ex) when (ex is DomainException or ArgumentException)
            {
                run.Error(row.Line, "coverage rules", ex.Message);
                continue;
            }

            if (parsed.Status == RuleStatus.Published && candidate.DurationMonths is null)
            {
                run.Error(row.Line, "coverage rules", "A published rule needs a verified duration in months.");
                continue;
            }

            var latestIsLive = latest?.Status is RuleStatus.Published or RuleStatus.Stale;
            var unchanged = latest is not null && candidate.HasSameContentAs(latest) && parsed.Status switch
            {
                RuleStatus.Published => latestIsLive,
                RuleStatus.Retired => latest.Status == RuleStatus.Retired,
                _ => latest.Status == RuleStatus.Draft,
            };

            var detail = $"v{(unchanged ? latest!.Version : next)}|{parsed.Status}|{candidate.DurationMonths}m|{candidate.CoverageType}";
            if (unchanged)
            {
                run.Change(RegistryEntity.CoverageRule, key, RegistryAction.Unchanged, detail);
                continue;
            }

            if (parsed.Status == RuleStatus.Retired)
            {
                if (latest is null)
                {
                    run.Error(row.Line, "coverage rules", "There is no rule to retire.");
                    continue;
                }

                run.Change(RegistryEntity.CoverageRule, key, RegistryAction.Update, $"v{latest.Version}|Retired");
                if (run.Apply)
                {
                    latest.Retire(run.Now);
                }

                continue;
            }

            run.Change(RegistryEntity.CoverageRule, key, latest is null ? RegistryAction.Add : RegistryAction.NewVersion, detail);
            if (!run.Apply)
            {
                continue;
            }

            db.Set<CoverageRuleVersion>().Add(candidate);
            if (parsed.Status == RuleStatus.Published)
            {
                foreach (var live in versions.Where(v => v.Status is RuleStatus.Published or RuleStatus.Stale))
                {
                    live.Supersede(run.Now);
                }

                candidate.Publish(run.Now);
            }
        }
    }

    private void ImportRegistrationRules(
        Run run, RegistrySnapshot snapshot, Dictionary<string, Brand> brands,
        Dictionary<SourceEvidenceId, SourceEvidence> sources, ILookup<Guid, RegistrationRuleVersion> existing)
    {
        var seen = new HashSet<string>();
        foreach (var row in snapshot.RegistrationRules ?? [])
        {
            var key = row.RuleKey?.Trim().ToLowerInvariant() ?? "";
            if (key.Length == 0 || !seen.Add(key))
            {
                run.Error(row.Line, "registration rules", $"Rule key '{row.RuleKey}' is blank or listed twice.");
                continue;
            }

            if (!TrySlug(row.Brand, out var brandSlug) || !brands.TryGetValue(brandSlug, out var brand))
            {
                run.Error(row.Line, "registration rules", $"Brand '{row.Brand}' is not in the registry.");
                continue;
            }

            var category = ParseEnum<CareCategory>(row.Category);
            var mode = ParseEnum<RegistrationMode>(row.Mode);
            var destinationType = string.IsNullOrWhiteSpace(row.DestinationType)
                ? RegistrationDestinationType.WebPage : ParseEnum<RegistrationDestinationType>(row.DestinationType);
            var confidence = ParseEnum<RegistryConfidence>(row.Confidence);
            var status = string.IsNullOrWhiteSpace(row.Status) ? RuleStatus.Draft : ParseEnum<RuleStatus>(row.Status);
            var fields = (row.RequiredFields ?? "").Split([';', '|'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Select(ParseEnum<RegistrationField>).ToList();

            if ((!string.IsNullOrWhiteSpace(row.Category) && category is null) || mode is null || destinationType is null
                || confidence is null || fields.Any(f => f is null)
                || status is not (RuleStatus.Draft or RuleStatus.Published or RuleStatus.Retired)
                || !TryInt(row.DeadlineDays, out var deadline) || !TryInt(row.ExtensionMonths, out var extension)
                || !TryDate(row.LastVerified, out var verified) || verified is null || string.IsNullOrWhiteSpace(row.SourceTitle))
            {
                run.Error(row.Line, "registration rules",
                    "Mode, destination type, category, required fields, confidence, status, a number, LastVerified or SourceTitle is not valid.");
                continue;
            }

            var source = Source(run, sources, row.SourceUrl, row.SourceTitle,
                string.IsNullOrWhiteSpace(row.SourceUrl) ? SourceType.Document : SourceType.WebPage, row.Line, row.SourceTitle);
            if (source is null)
            {
                continue;
            }

            var logical = DeterministicGuid.From($"{IdPrefix}registration-rule:{key}");
            var versions = existing[logical].OrderBy(v => v.Version).ToList();
            var latest = versions.LastOrDefault();
            var next = (latest?.Version ?? 0) + 1;

            RegistrationRuleVersion candidate;
            try
            {
                candidate = RegistrationRuleVersion.Draft(
                    new RegistrationRuleVersionId(DeterministicGuid.From($"{IdPrefix}registration-rule:{key}:v{next}")), logical, next,
                    string.IsNullOrWhiteSpace(row.Country) ? snapshot.CountryCode : row.Country.Trim().ToUpperInvariant(),
                    brand.Id, category, row.Model, mode.Value, destinationType.Value, row.Destination ?? "",
                    fields.Select(f => f!.Value), deadline, extension, row.Benefit ?? "", row.Instructions, confidence.Value,
                    source.Value, new DateTimeOffset(verified.Value.ToDateTime(TimeOnly.MinValue), TimeSpan.Zero));
            }
            catch (Exception ex) when (ex is DomainException or ArgumentException)
            {
                run.Error(row.Line, "registration rules", ex.Message);
                continue;
            }

            var latestIsLive = latest?.Status is RuleStatus.Published or RuleStatus.Stale;
            var unchanged = latest is not null && candidate.HasSameContentAs(latest) && status switch
            {
                RuleStatus.Published => latestIsLive,
                RuleStatus.Retired => latest.Status == RuleStatus.Retired,
                _ => latest.Status == RuleStatus.Draft,
            };

            var detail = $"v{(unchanged ? latest!.Version : next)}|{status}|{candidate.Mode}|{candidate.DeadlineDays}d";
            if (unchanged)
            {
                run.Change(RegistryEntity.RegistrationRule, key, RegistryAction.Unchanged, detail);
                continue;
            }

            if (status == RuleStatus.Retired)
            {
                if (latest is null)
                {
                    run.Error(row.Line, "registration rules", "There is no rule to retire.");
                    continue;
                }

                run.Change(RegistryEntity.RegistrationRule, key, RegistryAction.Update, $"v{latest.Version}|Retired");
                if (run.Apply)
                {
                    latest.Retire(run.Now);
                }

                continue;
            }

            run.Change(RegistryEntity.RegistrationRule, key, latest is null ? RegistryAction.Add : RegistryAction.NewVersion, detail);
            if (!run.Apply)
            {
                continue;
            }

            db.Set<RegistrationRuleVersion>().Add(candidate);
            if (status == RuleStatus.Published)
            {
                foreach (var live in versions.Where(v => v.Status is RuleStatus.Published or RuleStatus.Stale))
                {
                    live.Supersede(run.Now);
                }

                candidate.Publish(run.Now);
            }
        }
    }

    private sealed record ParsedRule(
        string Country, Brand? Brand, CareCategory? Category, Retailer? Retailer, CoverageType Type, int? Months,
        CoverageStartBasis StartBasis, bool RegistrationRequired, int? DeadlineDays, int? ExtensionMonths,
        DateOnly? EffectiveFrom, DateOnly? EffectiveTo, RegistryConfidence Confidence, DateTimeOffset LastVerified, RuleStatus Status);

    private static bool TryParseRule(
        CoverageRuleRow row, RegistrySnapshot snapshot, Dictionary<string, Brand> brands, Dictionary<string, Retailer> retailers,
        out ParsedRule parsed, out string problem)
    {
        parsed = null!;
        problem = "";

        Brand? brand = null;
        if (!string.IsNullOrWhiteSpace(row.Brand) && (!TrySlug(row.Brand, out var brandSlug) || !brands.TryGetValue(brandSlug, out brand)))
        {
            problem = $"Brand '{row.Brand}' is not in the registry.";
            return false;
        }

        Retailer? retailer = null;
        if (!string.IsNullOrWhiteSpace(row.Retailer) && (!TrySlug(row.Retailer, out var retailerSlug) || !retailers.TryGetValue(retailerSlug, out retailer)))
        {
            problem = $"Retailer '{row.Retailer}' is not in the registry.";
            return false;
        }

        var category = ParseEnum<CareCategory>(row.Category);
        var type = ParseEnum<CoverageType>(row.CoverageType);
        var basis = string.IsNullOrWhiteSpace(row.StartBasis) ? CoverageStartBasis.PurchaseDate : ParseEnum<CoverageStartBasis>(row.StartBasis);
        var confidence = ParseEnum<RegistryConfidence>(row.Confidence);
        var status = string.IsNullOrWhiteSpace(row.Status) ? RuleStatus.Draft : ParseEnum<RuleStatus>(row.Status);

        if ((!string.IsNullOrWhiteSpace(row.Category) && category is null) || type is null || basis is null || confidence is null
            || status is not (RuleStatus.Draft or RuleStatus.Published or RuleStatus.Retired))
        {
            problem = "Category, coverage type, start basis, confidence or status is not a known value.";
            return false;
        }

        if (!TryInt(row.DurationMonths, out var months) || !TryInt(row.RegistrationDeadlineDays, out var deadline)
            || !TryInt(row.ExtensionMonths, out var extension) || !TryDate(row.EffectiveFrom, out var from)
            || !TryDate(row.EffectiveTo, out var to) || !TryDate(row.LastVerified, out var verified) || verified is null)
        {
            problem = "A number or date is malformed, or LastVerified (yyyy-MM-dd) is missing.";
            return false;
        }

        var registration = row.RegistrationRequired?.Trim().ToLowerInvariant() is "yes" or "true" or "y";

        parsed = new ParsedRule(
            string.IsNullOrWhiteSpace(row.Country) ? snapshot.CountryCode : row.Country.Trim().ToUpperInvariant(),
            brand, category, retailer, type.Value, months, basis.Value, registration, deadline, extension, from, to,
            confidence.Value, new DateTimeOffset(verified.Value.ToDateTime(TimeOnly.MinValue), TimeSpan.Zero), status.Value);
        return true;
    }

    /// <summary>The source for a row, created on first sight. Null (with an error) when the URL is not a URL.</summary>
    private SourceEvidenceId? Source(
        Run run, Dictionary<SourceEvidenceId, SourceEvidence> sources, string? url, string title, SourceType type, int line,
        string? keyTitle = null)
    {
        var cleanUrl = string.IsNullOrWhiteSpace(url) ? null : url.Trim();
        if (cleanUrl is null && keyTitle is null)
        {
            return null;
        }

        var key = cleanUrl ?? $"title:{keyTitle!.Trim().ToLowerInvariant()}";
        var id = new SourceEvidenceId(DeterministicGuid.From($"{IdPrefix}source:{key}"));
        if (sources.ContainsKey(id))
        {
            return id;
        }

        try
        {
            var source = SourceEvidence.Record(id, type, title, cleanUrl, run.Snapshot.ResearchedAt);
            sources[id] = source;
            if (run.Apply)
            {
                db.Set<SourceEvidence>().Add(source);
            }

            run.Change(RegistryEntity.Source, key, RegistryAction.Add, source.Title);
            return id;
        }
        catch (Exception ex) when (ex is DomainException or ArgumentException)
        {
            run.Error(line, "sources", ex.Message);
            return null;
        }
    }

    private static bool TrySlug(string? name, out string slug)
    {
        slug = "";
        if (string.IsNullOrWhiteSpace(name) || !name.Any(char.IsAsciiLetterOrDigit))
        {
            return false;
        }

        slug = RegistrySlug.For(name);
        return true;
    }

    private static TEnum? ParseEnum<TEnum>(string? value) where TEnum : struct, Enum
    {
        var compact = value?.Replace(" ", "").Replace("-", "").Replace("_", "");
        return Enum.TryParse<TEnum>(compact, ignoreCase: true, out var parsed) && Enum.IsDefined(parsed) ? parsed : null;
    }

    private static bool TryInt(string? value, out int? result)
    {
        result = null;
        if (string.IsNullOrWhiteSpace(value))
        {
            return true;
        }

        if (int.TryParse(value.Trim(), NumberStyles.None, CultureInfo.InvariantCulture, out var parsed))
        {
            result = parsed;
            return true;
        }

        return false;
    }

    private static bool TryDate(string? value, out DateOnly? result)
    {
        result = null;
        if (string.IsNullOrWhiteSpace(value))
        {
            return true;
        }

        if (DateOnly.TryParseExact(value.Trim(), "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsed))
        {
            result = parsed;
            return true;
        }

        return false;
    }

    private sealed class Run(RegistrySnapshot snapshot, bool apply, DateTimeOffset now)
    {
        public RegistrySnapshot Snapshot { get; } = snapshot;
        public bool Apply { get; } = apply;
        public DateTimeOffset Now { get; } = now;
        public List<RegistryChange> Changes { get; } = [];
        public List<string> Errors { get; } = [];

        public void Change(RegistryEntity entity, string key, RegistryAction action, string detail) =>
            Changes.Add(new RegistryChange(entity, key, action, detail));

        public void Error(int line, string file, string message) => Errors.Add($"{file}, line {line}: {message}");
    }
}
