using FluentValidation;
using Kadari.Application.Features.Care.Services;
using Kadari.Domain.Care;

namespace Kadari.Application.Features.Care.RecordProviderUpdate;

internal sealed class RecordProviderUpdateValidator : AbstractValidator<RecordProviderUpdateCommand>
{
    public RecordProviderUpdateValidator()
    {
        RuleFor(x => x.CustomerId).NotEmpty();
        RuleFor(x => x.AssetId).NotEmpty();
        RuleFor(x => x.CaseId).NotEmpty();
        RuleFor(x => x.Status).Must(s => CareEnum.IsName<CaseStatus>(s)).WithMessage($"Status must be one of: {CareEnum.Names<CaseStatus>()}.");
        RuleFor(x => x.Note).MaximumLength(CaseMessage.MaxBodyLength);
    }
}
