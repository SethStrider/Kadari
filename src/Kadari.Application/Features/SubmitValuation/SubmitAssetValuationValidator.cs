using FluentValidation;

namespace Kadari.Application.Features.SubmitValuation;

internal sealed class SubmitAssetValuationValidator : AbstractValidator<SubmitAssetValuationCommand>
{
    public SubmitAssetValuationValidator()
    {
        RuleFor(x => x.AssetId).NotEmpty();
        RuleFor(x => x.AmountMinor).GreaterThan(0).WithMessage("A valuation amount must be positive.");
        RuleFor(x => x.Currency).Length(3).WithMessage("Currency must be an ISO-4217 alpha-3 code.");
        RuleFor(x => x.Valuer).NotEmpty().MaximumLength(80);
        RuleFor(x => x.CertificateReference).NotEmpty().MaximumLength(512);
    }
}
