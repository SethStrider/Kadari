using FluentValidation;
using Kadari.Domain.CareRegistry;

namespace Kadari.Application.Features.Care.EnableCare;

internal sealed class EnableCareValidator : AbstractValidator<EnableCareCommand>
{
    public EnableCareValidator()
    {
        RuleFor(x => x.CustomerId).NotEmpty();
        RuleFor(x => x.AssetId).NotEmpty();
        RuleFor(x => x.Category)
            .NotEmpty()
            .Must(c => !int.TryParse(c, out _) && Enum.TryParse<CareCategory>(c, ignoreCase: true, out var v) && Enum.IsDefined(v) && !c.Contains(','))
            .WithMessage($"Category must be one of: {string.Join(", ", Enum.GetNames<CareCategory>())}.");
    }
}
