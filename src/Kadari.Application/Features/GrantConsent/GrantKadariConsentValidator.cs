using FluentValidation;

namespace Kadari.Application.Features.GrantConsent;

internal sealed class GrantKadariConsentValidator : AbstractValidator<GrantKadariConsentCommand>
{
    public GrantKadariConsentValidator()
    {
        RuleFor(x => x.CustomerId).NotEmpty();
        RuleFor(x => x.Scope).IsInEnum();
    }
}
