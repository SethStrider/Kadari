using Kadari.Domain.Assets;
using Kadari.Domain.CareRegistry;
using Kadari.Domain.Customers;

namespace Kadari.Application.Features.Care.Services;

/// <summary>One fact a registration would share, as the owner sees it before authorising (CARE/05 C04, C14).</summary>
public sealed record SharedFieldDto(string Field, string Label, string? Value);

/// <summary>
/// Exactly what a registration would send, built from the rule's required fields only — nothing else about the owner
/// or the household leaves KADARI (CARE/16 §2.15, CARE/13 §23). Shown before authorising; the authorisation records
/// the same list.
/// </summary>
public static class RegistrationPack
{
    /// <summary>The wording the owner agrees to. Bump the version whenever the wording changes.</summary>
    public const string TermsVersion = "care-registration-authority-v1";

    public const string Terms =
        "I authorise KADARI to prepare my warranty registration and to share the details listed with the provider named, " +
        "for this registration only.";

    private static readonly Dictionary<RegistrationField, string> Labels = new()
    {
        [RegistrationField.OwnerName] = "Your name",
        [RegistrationField.Email] = "Your email",
        [RegistrationField.Mobile] = "Your mobile number",
        [RegistrationField.Brand] = "Brand",
        [RegistrationField.Model] = "Model",
        [RegistrationField.SerialNumber] = "Serial number",
        [RegistrationField.PurchaseDate] = "Purchase date",
        [RegistrationField.Retailer] = "Bought from",
        [RegistrationField.ProofOfPurchase] = "Proof of purchase",
    };

    public static IReadOnlyList<SharedFieldDto> For(RegistrationRuleVersion rule, Customer customer, Asset asset)
    {
        var facts = asset.LatestPurchase?.Purchase;
        var serial = asset.Evidence
            .Where(e => e.Kind == AssetEvidenceKind.Ownership && e.Source == "serial" && e.Reference != null)
            .OrderByDescending(e => e.AssertedAt)
            .Select(e => e.Reference)
            .FirstOrDefault();
        var receiptHeld = asset.Evidence.Any(e => e.Kind == AssetEvidenceKind.Ownership && e.Source == "receipt" && e.Reference != null);

        return rule.RequiredFields.Select(field => new SharedFieldDto(field.ToString(), Labels[field], field switch
        {
            RegistrationField.OwnerName => $"{customer.FirstName} {customer.LastName}".Trim(),
            RegistrationField.Email => customer.Email,
            RegistrationField.Mobile => customer.MobileNumber,
            RegistrationField.Brand => facts?.Brand,
            RegistrationField.Model => facts?.Model,
            RegistrationField.SerialNumber => serial,
            RegistrationField.PurchaseDate => facts?.PurchasedOn?.ToString("yyyy-MM-dd"),
            RegistrationField.Retailer => facts?.Retailer,
            RegistrationField.ProofOfPurchase => receiptHeld ? "Your receipt, as KADARI holds it" : null,
            _ => null,
        })).ToList();
    }
}
