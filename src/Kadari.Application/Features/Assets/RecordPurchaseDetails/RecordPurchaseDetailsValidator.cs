using FluentValidation;
using Kadari.Domain.Assets;

namespace Kadari.Application.Features.Assets.RecordPurchaseDetails;

internal sealed class RecordPurchaseDetailsValidator : AbstractValidator<RecordPurchaseDetailsCommand>
{
    public RecordPurchaseDetailsValidator(TimeProvider clock)
    {
        RuleFor(x => x.CustomerId).NotEmpty();
        RuleFor(x => x.AssetId).NotEmpty();

        RuleFor(x => x)
            .Must(x => !string.IsNullOrWhiteSpace(x.Brand) || !string.IsNullOrWhiteSpace(x.Model)
                       || !string.IsNullOrWhiteSpace(x.Retailer) || x.PurchasedOn is not null)
            .WithMessage("Provide at least one purchase detail.");

        RuleFor(x => x.Brand).MaximumLength(PurchaseFacts.MaxTextLength);
        RuleFor(x => x.Model).MaximumLength(PurchaseFacts.MaxTextLength);
        RuleFor(x => x.Retailer).MaximumLength(PurchaseFacts.MaxTextLength);

        // A purchase date in the future, or before consumer electronics existed, is a typo.
        RuleFor(x => x.PurchasedOn)
            .Must(date => date <= DateOnly.FromDateTime(clock.GetUtcNow().UtcDateTime.AddDays(1)))
            .When(x => x.PurchasedOn is not null)
            .WithMessage("The purchase date cannot be in the future.");
        RuleFor(x => x.PurchasedOn)
            .GreaterThanOrEqualTo(new DateOnly(1950, 1, 1))
            .When(x => x.PurchasedOn is not null);
    }
}
