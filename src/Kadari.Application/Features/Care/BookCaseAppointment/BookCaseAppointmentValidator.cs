using FluentValidation;
using Kadari.Application.Features.Care.Services;
using Kadari.Domain.Care;

namespace Kadari.Application.Features.Care.BookCaseAppointment;

internal sealed class BookCaseAppointmentValidator : AbstractValidator<BookCaseAppointmentCommand>
{
    public BookCaseAppointmentValidator()
    {
        RuleFor(x => x.CustomerId).NotEmpty();
        RuleFor(x => x.AssetId).NotEmpty();
        RuleFor(x => x.CaseId).NotEmpty();
        RuleFor(x => x.Location).MaximumLength(CaseAppointment.MaxTextLength);
        RuleFor(x => x.Instructions).MaximumLength(CaseAppointment.MaxTextLength);
    }
}
