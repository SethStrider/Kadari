using Kadari.Domain.Care;

namespace Kadari.Application.Features.Care.Services;

/// <summary>
/// What a reminder says (CARE/03 §9): why the owner should care, the consequence, and what to do — never a bare
/// countdown. "Your Bosch dishwasher warranty expires in 30 days. If you've noticed anything unusual, now is the time to
/// check it."
/// </summary>
public static class ReminderMessages
{
    public static string Headline(CareReminderType type) => type switch
    {
        CareReminderType.RegistrationDeadline => "Register for your extra cover",
        CareReminderType.Warranty90 => "Your warranty ends in about three months",
        CareReminderType.Warranty30 => "Your warranty ends in 30 days",
        CareReminderType.CaseFollowUp => "Follow up on your case",
        CareReminderType.CommitmentOverdue => "No reply yet",
        CareReminderType.AppointmentDue => "Appointment coming up",
        _ => "KADARI Care",
    };

    public static string Body(CareReminderType type, string assetName, DateOnly? keyDate, string? link)
    {
        var date = keyDate?.ToString("d MMMM yyyy");
        var text = type switch
        {
            CareReminderType.RegistrationDeadline =>
                $"Your {assetName} can get extra cover if you register{(date is null ? "" : $" by {date}")}. KADARI has the details ready.",
            CareReminderType.Warranty90 =>
                $"Your {assetName} warranty ends on {date}. If anything isn't working as it should, there's still time to have it fixed under warranty.",
            CareReminderType.Warranty30 =>
                $"Your {assetName} warranty expires in 30 days, on {date}. If you've noticed anything unusual, now is the time to check it.",
            CareReminderType.CaseFollowUp => $"Your case about your {assetName} is waiting for an update.",
            CareReminderType.CommitmentOverdue => $"The provider hasn't replied about your {assetName} by the date they promised. KADARI has a follow-up ready.",
            CareReminderType.AppointmentDue => $"Your appointment for your {assetName} is coming up{(date is null ? "" : $" on {date}")}.",
            _ => $"There's an update about your {assetName}.",
        };

        return link is null ? $"KADARI: {text}" : $"KADARI: {text} {link}";
    }
}
