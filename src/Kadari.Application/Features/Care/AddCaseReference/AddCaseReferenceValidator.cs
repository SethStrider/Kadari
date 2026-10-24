using FluentValidation;
using Kadari.Application.Features.Care.Services;
using Kadari.Domain.Care;

namespace Kadari.Application.Features.Care.AddCaseReference;

internal sealed class AddCaseReferenceValidator : AbstractValidator<AddCaseReferenceCommand>
{
    public AddCaseReferenceValidator()
    {
        RuleFor(x => x.CustomerId).NotEmpty();
        RuleFor(x => x.AssetId).NotEmpty();
        RuleFor(x => x.CaseId).NotEmpty();
        RuleFor(x => x.Party).NotEmpty().MaximumLength(CaseReference.MaxLength);
        RuleFor(x => x.Reference).NotEmpty().MaximumLength(CaseReference.MaxLength);
    }
}
