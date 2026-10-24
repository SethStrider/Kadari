using FluentValidation;

namespace Kadari.Application.Features.Care.AuthoriseRegistration;

internal sealed class AuthoriseRegistrationValidator : AbstractValidator<AuthoriseRegistrationCommand>
{
    public AuthoriseRegistrationValidator()
    {
        RuleFor(x => x.CustomerId).NotEmpty();
        RuleFor(x => x.AssetId).NotEmpty();
        RuleFor(x => x.OpportunityId).NotEmpty();
        RuleFor(x => x.AcceptedTermsVersion).NotEmpty();
        RuleFor(x => x.ExplicitConsent).Equal(true).WithMessage("Registration needs your explicit authority.");
        RuleFor(x => x.IdempotencyKey).NotEmpty().MaximumLength(100);
    }
}
