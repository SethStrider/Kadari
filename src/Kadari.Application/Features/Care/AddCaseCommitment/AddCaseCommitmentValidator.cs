using FluentValidation;
using Kadari.Application.Features.Care.Services;
using Kadari.Domain.Care;

namespace Kadari.Application.Features.Care.AddCaseCommitment;

internal sealed class AddCaseCommitmentValidator : AbstractValidator<AddCaseCommitmentCommand>
{
    public AddCaseCommitmentValidator()
    {
        RuleFor(x => x.CustomerId).NotEmpty();
        RuleFor(x => x.AssetId).NotEmpty();
        RuleFor(x => x.CaseId).NotEmpty();
        RuleFor(x => x.Party).Must(s => CareEnum.IsName<CommitmentParty>(s)).WithMessage($"Party must be one of: {CareEnum.Names<CommitmentParty>()}.");
        RuleFor(x => x.Description).NotEmpty().MaximumLength(CaseCommitment.MaxDescriptionLength);
    }
}
