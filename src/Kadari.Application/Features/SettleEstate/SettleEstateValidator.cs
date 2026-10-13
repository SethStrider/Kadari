using FluentValidation;

namespace Kadari.Application.Features.SettleEstate;

internal sealed class SettleEstateValidator : AbstractValidator<SettleEstateCommand>
{
    public SettleEstateValidator()
    {
        RuleFor(x => x.DeceasedCustomerId).NotEmpty();
        RuleFor(x => x.HeirCustomerId).NotEmpty();
        RuleFor(x => x.HeirCustomerId)
            .NotEqual(x => x.DeceasedCustomerId)
            .WithMessage("The heir must differ from the deceased.");
    }
}
