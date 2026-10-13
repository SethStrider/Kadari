using FluentValidation;

namespace Kadari.Application.Features.CreateInsurerTenant;

internal sealed class CreateInsurerTenantValidator : AbstractValidator<CreateInsurerTenantCommand>
{
    public const string HexColor = "^#[0-9a-fA-F]{6}$";

    public CreateInsurerTenantValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(200);

        When(x => x.Branding is not null, () =>
        {
            RuleFor(x => x.Branding!.PrimaryColor).Matches(HexColor).WithMessage("PrimaryColor must be a #RRGGBB hex colour.");
            RuleFor(x => x.Branding!.AccentColor).Matches(HexColor).WithMessage("AccentColor must be a #RRGGBB hex colour.");
            RuleFor(x => x.Branding!.LogoReference).MaximumLength(512).When(x => x.Branding!.LogoReference is not null);
        });
    }
}
