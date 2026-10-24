using FluentValidation;

namespace Kadari.Application.Features.Care.MarkCaseSent;

internal sealed class MarkCaseSentValidator : AbstractValidator<MarkCaseSentCommand>
{
    public MarkCaseSentValidator()
    {
        RuleFor(x => x.CustomerId).NotEmpty();
        RuleFor(x => x.AssetId).NotEmpty();
        RuleFor(x => x.CaseId).NotEmpty();
        RuleFor(x => x.AcceptedTermsVersion).NotEmpty();
        RuleFor(x => x.ExplicitConsent).Equal(true).WithMessage("Sending needs your explicit agreement.");
        RuleFor(x => x.IdempotencyKey).NotEmpty().MaximumLength(100);
    }
}
