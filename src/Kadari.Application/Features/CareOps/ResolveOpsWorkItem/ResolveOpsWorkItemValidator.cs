using FluentValidation;
using Kadari.Application.Features.Care.Services;
using Kadari.Domain.Care;

namespace Kadari.Application.Features.CareOps.ResolveOpsWorkItem;

internal sealed class ResolveOpsWorkItemValidator : AbstractValidator<ResolveOpsWorkItemCommand>
{
    public ResolveOpsWorkItemValidator()
    {
        RuleFor(x => x.WorkItemId).NotEmpty();
        RuleFor(x => x.Resolution)
            .Must(r => CareEnum.IsName<OpsResolution>(r))
            .WithMessage($"Resolution must be one of: {CareEnum.Names<OpsResolution>()}.");
        RuleFor(x => x.Notes).NotEmpty().MaximumLength(OpsWorkItem.MaxNotesLength);
    }
}
