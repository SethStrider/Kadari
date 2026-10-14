using Kadari.Domain.Customers;
using Kadari.Domain.Assets;
using Kadari.Infrastructure.Persistence;

namespace Kadari.Infrastructure.Tests.Persistence;

internal static class KadariCaptureSeed
{
    /// <summary>Adds the customer with the image-processing consent capture requires (AS-02).</summary>
    public static void AddCustomerConsentingToCapture(this KadariDbContext db, Customer customer)
    {
        db.Customers.Add(customer);
        db.KadariConsents.Add(KadariConsent.Grant(
            customer.Id, KadariConsentScope.AssetImageProcessing, TimeProvider.System.GetUtcNow()));
    }
}
