using FluentValidation;

namespace Kadari.Application.Features.MintAssetPassport;

internal sealed class MintAssetPassportValidator : AbstractValidator<MintAssetPassportCommand>
{
    public MintAssetPassportValidator()
    {
        RuleFor(x => x.CustomerId).NotEmpty();
        RuleFor(x => x.AssetId).NotEmpty();
    }
}
