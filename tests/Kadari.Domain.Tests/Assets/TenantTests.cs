using Kadari.Domain.Assets;

namespace Kadari.Domain.Tests.Assets;

public class TenantTests
{
    [Fact]
    public void CreatePlatformDefault_is_the_well_known_active_Trustli_tenant()
    {
        var tenant = Tenant.CreatePlatformDefault();

        Assert.Equal(TenantId.Default, tenant.Id);
        Assert.Equal(TenantKind.Platform, tenant.Kind);
        Assert.Equal("KADARI", tenant.Name);
        Assert.Equal(TenantStatus.Active, tenant.Status);
    }

    [Fact]
    public void CreateInsurer_is_an_active_insurer_with_a_fresh_id()
    {
        var tenant = Tenant.CreateInsurer("  Acme Insurance  ");

        Assert.Equal(TenantKind.Insurer, tenant.Kind);
        Assert.Equal("Acme Insurance", tenant.Name);
        Assert.Equal(TenantStatus.Active, tenant.Status);
        Assert.NotEqual(TenantId.Default, tenant.Id);
    }

    [Fact]
    public void CreateInsurer_rejects_a_blank_name()
    {
        Assert.Throws<ArgumentException>(() => Tenant.CreateInsurer("   "));
    }

    [Fact]
    public void Suspend_then_activate_round_trips_status()
    {
        var tenant = Tenant.CreateInsurer("Acme Insurance");

        tenant.Suspend();
        Assert.Equal(TenantStatus.Suspended, tenant.Status);

        tenant.Activate();
        Assert.Equal(TenantStatus.Active, tenant.Status);
    }

    [Fact]
    public void A_new_tenant_has_no_branding()
    {
        var tenant = Tenant.CreateInsurer("Acme Insurance");

        Assert.Null(tenant.Branding);
    }

    [Fact]
    public void Rebrand_then_clear_round_trips_branding()
    {
        var tenant = Tenant.CreateInsurer("Acme Insurance");

        tenant.Rebrand(new TenantBranding("#123abc", "#00DCC2", "brand/acme-logo.png"));

        Assert.NotNull(tenant.Branding);
        Assert.Equal("#123ABC", tenant.Branding!.PrimaryColor);   // normalised to upper-case
        Assert.Equal("#00DCC2", tenant.Branding.AccentColor);
        Assert.Equal("brand/acme-logo.png", tenant.Branding.LogoReference);

        tenant.ClearBranding();
        Assert.Null(tenant.Branding);
    }
}

public class TenantBrandingTests
{
    [Fact]
    public void Colours_are_normalised_to_upper_case_hex()
    {
        var branding = new TenantBranding("  #a1b2c3  ", "#FFFFFF", null);

        Assert.Equal("#A1B2C3", branding.PrimaryColor);
        Assert.Equal("#FFFFFF", branding.AccentColor);
        Assert.Null(branding.LogoReference);
    }

    [Theory]
    [InlineData("123abc")]      // missing #
    [InlineData("#123")]        // shorthand not accepted
    [InlineData("#12abcz")]     // non-hex digit
    [InlineData("#1234567")]    // too long
    public void A_malformed_colour_is_rejected(string bad)
    {
        Assert.Throws<ArgumentException>(() => new TenantBranding(bad, "#000000", null));
    }

    [Fact]
    public void A_blank_logo_reference_is_rejected()
    {
        Assert.Throws<ArgumentException>(() => new TenantBranding("#000000", "#ffffff", "   "));
    }
}
