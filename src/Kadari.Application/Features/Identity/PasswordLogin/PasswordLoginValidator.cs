using FluentValidation;

namespace Kadari.Application.Features.Identity.PasswordLogin;

internal sealed class PasswordLoginValidator : AbstractValidator<PasswordLoginCommand>
{
    public PasswordLoginValidator()
    {
        RuleFor(x => x.Email).NotEmpty().EmailAddress().MaximumLength(320);
        RuleFor(x => x.Password).NotEmpty();
    }
}
