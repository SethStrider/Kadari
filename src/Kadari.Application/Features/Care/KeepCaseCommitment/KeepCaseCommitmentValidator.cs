using FluentValidation;
using Kadari.Application.Features.Care.Services;
using Kadari.Domain.Care;

namespace Kadari.Application.Features.Care.KeepCaseCommitment;

internal sealed class KeepCaseCommitmentValidator : AbstractValidator<KeepCaseCommitmentCommand>
{
    public KeepCaseCommitmentValidator()
    {
        RuleFor(x => x.CustomerId).NotEmpty();
        RuleFor(x => x.AssetId).NotEmpty();
        RuleFor(x => x.CaseId).NotEmpty();
        RuleFor(x => x.CommitmentId).NotEmpty();
    }
}
