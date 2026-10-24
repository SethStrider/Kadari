using FluentValidation;
using Kadari.Application.Features.Care.Services;
using Kadari.Domain.Care;

namespace Kadari.Application.Features.CareOps.GetOpsQueue;

internal sealed class GetOpsQueueValidator : AbstractValidator<GetOpsQueueQuery>
{
    public GetOpsQueueValidator()
    {
        RuleFor(x => x.WorkType)
            .Must(t => t is null || CareEnum.IsName<OpsWorkType>(t))
            .WithMessage($"WorkType must be one of: {CareEnum.Names<OpsWorkType>()}.");
        RuleFor(x => x.Take).InclusiveBetween(1, 500);
    }
}
