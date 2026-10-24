using System.Security.Cryptography;
using System.Text;

namespace Kadari.Application.Features.CareRegistry.Services;

public enum RegistryEntity
{
    Source,
    Brand,
    Retailer,
    RoutingModel,
    CoverageRule,
    RegistrationRule,
    RoutingRule,
}

public enum RegistryAction
{
    Add,
    Update,
    NewVersion,
    Unchanged,
}

public sealed record RegistryChange(RegistryEntity Entity, string Key, RegistryAction Action, string Detail);

/// <summary>
/// What an import would do, row by row, and whether it may be applied. The <see cref="Fingerprint"/> covers every
/// change that is not <see cref="RegistryAction.Unchanged"/>: apply recomputes the plan inside its transaction and
/// refuses when the fingerprint differs, so what is applied is exactly what was previewed.
/// </summary>
public sealed record RegistryImportPlan(IReadOnlyList<RegistryChange> Changes, IReadOnlyList<string> Errors)
{
    public bool CanApply => Errors.Count == 0;

    public int Count(RegistryAction action) => Changes.Count(c => c.Action == action);

    public string Fingerprint
    {
        get
        {
            var material = Changes
                .Where(c => c.Action != RegistryAction.Unchanged)
                .Select(c => $"{c.Entity}|{c.Key}|{c.Action}|{c.Detail}");
            var hash = SHA256.HashData(Encoding.UTF8.GetBytes(string.Join("\n", material)));
            return Convert.ToHexStringLower(hash);
        }
    }
}
