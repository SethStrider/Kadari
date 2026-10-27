using FluentValidation;
using Kadari.Application.Features.Care.Services;
using Kadari.Domain.Care;

namespace Kadari.Application.Features.Care.ResolveCase;

internal sealed class ResolveCaseValidator : AbstractValidator<ResolveCaseCommand>
{
    public ResolveCaseValidator()
    {
        RuleFor(x => x.CustomerId).NotEmpty();
        RuleFor(x => x.AssetId).NotEmpty();
        RuleFor(x => x.CaseId).NotEmpty();
        RuleFor(x => x.Outcome).Must(s => CareEnum.IsName<CaseOutcome>(s)).WithMessage($"Outcome must be one of: {CareEnum.Names<CaseOutcome>()}.");
        RuleFor(x => x.Notes).MaximumLength(AssetCase.MaxNotesLength);
    }
}
