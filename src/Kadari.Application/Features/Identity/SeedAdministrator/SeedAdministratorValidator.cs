using FluentValidation;

namespace Kadari.Application.Features.Identity.SeedAdministrator;

internal sealed class SeedAdministratorValidator : AbstractValidator<SeedAdministratorCommand>
{
    public SeedAdministratorValidator()
    {
        RuleFor(x => x.MobileNumber).NotEmpty().MaximumLength(32);
        RuleFor(x => x.Email).EmailAddress().MaximumLength(320).When(x => !string.IsNullOrWhiteSpace(x.Email));
    }
}
