using Kadari.Domain.Assets;
using Kadari.Domain.Customers;

namespace Kadari.Domain.Tests.Assets;

public class KadariConsentTests
{
    private static readonly DateTimeOffset Now = DateTimeOffset.UnixEpoch;
    private static readonly CustomerId Customer = new(Guid.CreateVersion7());

    [Fact]
    public void Grant_starts_active_for_the_scope()
    {
        var consent = KadariConsent.Grant(Customer, KadariConsentScope.CrossBorderAiEnrichment, Now);

        Assert.Equal(Customer, consent.CustomerId);
        Assert.Equal(KadariConsentScope.CrossBorderAiEnrichment, consent.Scope);
        Assert.Equal(KadariConsentStatus.Granted, consent.Status);
        Assert.True(consent.IsActive);
        Assert.Null(consent.WithdrawnAt);
    }

    [Fact]
    public void Withdraw_deactivates_the_consent()
    {
        var consent = KadariConsent.Grant(Customer, KadariConsentScope.AssetImageProcessing, Now);

        consent.Withdraw(Now.AddDays(1));

        Assert.Equal(KadariConsentStatus.Withdrawn, consent.Status);
        Assert.False(consent.IsActive);
        Assert.Equal(Now.AddDays(1), consent.WithdrawnAt);
    }

    [Fact]
    public void Regrant_reactivates_a_withdrawn_consent()
    {
        var consent = KadariConsent.Grant(Customer, KadariConsentScope.CrossBorderAiEnrichment, Now);
        consent.Withdraw(Now.AddDays(1));

        consent.Regrant(Now.AddDays(2));

        Assert.True(consent.IsActive);
        Assert.Null(consent.WithdrawnAt);
        Assert.Equal(Now.AddDays(2), consent.GrantedAt);
    }
}
