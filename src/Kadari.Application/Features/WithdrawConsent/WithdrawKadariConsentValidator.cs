using FluentValidation;

namespace Kadari.Application.Features.WithdrawConsent;

internal sealed class WithdrawKadariConsentValidator : AbstractValidator<WithdrawKadariConsentCommand>
{
    public WithdrawKadariConsentValidator()
    {
        RuleFor(x => x.CustomerId).NotEmpty();
        RuleFor(x => x.Scope).IsInEnum();
    }
}
