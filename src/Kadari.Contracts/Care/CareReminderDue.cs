namespace Kadari.Contracts.Care;

/// <summary>
/// A Care reminder's date has come and it should be delivered (CARE/14 C6). Published through the outbox by the
/// reminder sweep; the Worker's consumer looks up who to tell and how, sends it, and records the outcome. The message id
/// is derived from the reminder, so a re-run sweep cannot send it twice.
/// </summary>
public sealed record CareReminderDue(Guid ReminderId);
