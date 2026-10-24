using FluentValidation;

namespace Kadari.Application.Features.Care.SnoozeCareReminder;

internal sealed class SnoozeCareReminderValidator : AbstractValidator<SnoozeCareReminderCommand>
{
    public SnoozeCareReminderValidator()
    {
        RuleFor(x => x.ReminderId).NotEmpty();
        RuleFor(x => x.Days).InclusiveBetween(1, 90);
    }
}
