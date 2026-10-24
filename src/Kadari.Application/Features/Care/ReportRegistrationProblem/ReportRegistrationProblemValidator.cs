using FluentValidation;
using Kadari.Domain.Care;

namespace Kadari.Application.Features.Care.ReportRegistrationProblem;

internal sealed class ReportRegistrationProblemValidator : AbstractValidator<ReportRegistrationProblemCommand>
{
    public ReportRegistrationProblemValidator()
    {
        RuleFor(x => x.RegistrationId).NotEmpty();
        RuleFor(x => x.Reason).NotEmpty().MaximumLength(CoverageRegistration.MaxReasonLength);
    }
}
