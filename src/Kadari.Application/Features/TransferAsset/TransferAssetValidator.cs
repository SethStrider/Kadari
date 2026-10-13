using FluentValidation;

namespace Kadari.Application.Features.TransferAsset;

internal sealed class TransferAssetValidator : AbstractValidator<TransferAssetCommand>
{
    public TransferAssetValidator()
    {
        RuleFor(x => x.CustomerId).NotEmpty();
        RuleFor(x => x.AssetId).NotEmpty();
        RuleFor(x => x.RecipientCustomerId).NotEmpty();
        RuleFor(x => x.RecipientCustomerId)
            .NotEqual(x => x.CustomerId)
            .WithMessage("Cannot transfer an asset to yourself.");
    }
}
