using FluentValidation;
using Kadari.Application.Features.Care.Services;
using Kadari.Domain.Care;

namespace Kadari.Application.Features.Care.SettleCaseAppointment;

internal sealed class SettleCaseAppointmentValidator : AbstractValidator<SettleCaseAppointmentCommand>
{
    public SettleCaseAppointmentValidator()
    {
        RuleFor(x => x.CustomerId).NotEmpty();
        RuleFor(x => x.AssetId).NotEmpty();
        RuleFor(x => x.CaseId).NotEmpty();
        RuleFor(x => x.AppointmentId).NotEmpty();
    }
}
