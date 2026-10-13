using FluentValidation;

namespace Kadari.Application.Features.SetTenantBranding;

internal sealed class SetTenantBrandingValidator : AbstractValidator<SetTenantBrandingCommand>
{
    public const string HexColor = "^#[0-9a-fA-F]{6}$";

    public SetTenantBrandingValidator()
    {
        RuleFor(x => x.TenantId).NotEmpty();
        RuleFor(x => x.PrimaryColor).Matches(HexColor).WithMessage("PrimaryColor must be a #RRGGBB hex colour.");
        RuleFor(x => x.AccentColor).Matches(HexColor).WithMessage("AccentColor must be a #RRGGBB hex colour.");
        RuleFor(x => x.LogoReference).MaximumLength(512).When(x => x.LogoReference is not null);
    }
}
