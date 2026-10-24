using Kadari.Domain.Abstractions;
using Kadari.Domain.Assets;
using Kadari.Domain.Care;
using Kadari.Domain.CareRegistry;
using Kadari.Domain.Tests.CareRegistry;

namespace Kadari.Domain.Tests.Care;

/// <summary>Reminders (CARE/03 §8, CARE/14 C6): a deterministic schedule, sent once, never about something that stopped
/// being true.</summary>
public class CareReminderTests
{
    private static readonly DateTimeOffset Now = RuleFactory.Now;   // 24 Oct 2026
    private static readonly DateOnly Today = DateOnly.FromDateTime(Now.UtcDateTime);
    private static readonly AssetId Asset = AssetId.New();

    private static CareProfile ProfileWith(CoverageState state, DateOnly expires)
    {
        var profile = CareProfile.Enable(Asset, CareCategory.MajorAppliance, Now);
        var finding = new CoverageFinding(CoverageType.Manufacturer, state, null, null, null, false, 0, null, expires, false, null, null, "t");
        var evaluation = new CoverageEvaluation(state, [finding], "t");
        profile.ApplyEvaluation(evaluation, CoverageDecision.Record(Asset, evaluation, "{}", Now), Now);
        return profile;
    }

    [Fact]
    public void Plan_ForActiveCover_Schedules90And30DaysBeforeExpiry()
    {
        var plan = ReminderPlan.For(ProfileWith(CoverageState.Active, new DateOnly(2027, 6, 1)), [], Today);

        Assert.Equal(
            [(CareReminderType.Warranty90, new DateOnly(2027, 3, 3)), (CareReminderType.Warranty30, new DateOnly(2027, 5, 2))],
            plan.Select(p => (p.Type, p.DueOn)));
    }

    [Fact]
    public void Plan_ForCoverKadariCannotConfirm_SchedulesNothing()
        => Assert.Empty(ReminderPlan.For(ProfileWith(CoverageState.LikelyActive, new DateOnly(2027, 6, 1)), [], Today));

    [Fact]
    public void Plan_LeavesOutDatesThatHavePassed()
    {
        var plan = ReminderPlan.For(ProfileWith(CoverageState.Expiring, new DateOnly(2026, 12, 1)), [], Today);

        Assert.Equal([CareReminderType.Warranty30], plan.Select(p => p.Type));
    }

    [Fact]
    public void Plan_ForARegistrationOffer_RemindsHalfwayAndThreeDaysBefore()
    {
        var rule = RegistrationRuleVersion.Draft(
            new RegistrationRuleVersionId(Guid.CreateVersion7()), Guid.CreateVersion7(), 1, "ZA", RuleFactory.Bosch, null, null,
            RegistrationMode.Guided, RegistrationDestinationType.WebPage, "https://example.com/register", [], 28, null,
            "Register for extra cover.", null, RegistryConfidence.High, RuleFactory.Source, Now);
        rule.Publish(Now);
        var offer = RegistrationOpportunity.Surface(Asset, rule, Today, Now);   // deadline 21 Nov 2026

        var plan = ReminderPlan.For(CareProfile.Enable(Asset, CareCategory.MajorAppliance, Now), [offer], Today);

        Assert.Equal([new DateOnly(2026, 11, 7), new DateOnly(2026, 11, 18)], plan.Select(p => p.DueOn));
    }

    [Fact]
    public void Queue_BeforeTheDueDate_DoesNothing()
    {
        var reminder = CareReminder.Schedule(Asset, CareReminderType.Warranty30, Today.AddDays(1), "CareProfile", Guid.NewGuid());

        Assert.Equal((false, CareReminderStatus.Scheduled), (reminder.Queue(Today, Now), reminder.Status));
    }

    [Fact]
    public void MarkSent_IsOnceOnly()
    {
        var reminder = CareReminder.Schedule(Asset, CareReminderType.Warranty30, Today, "CareProfile", Guid.NewGuid());
        reminder.Queue(Today, Now);
        reminder.MarkSent("Email", Now);

        Assert.False(reminder.MarkSent("Email", Now));
    }

    [Fact]
    public void Cancel_LeavesADeliveredReminderAlone()
    {
        var reminder = CareReminder.Schedule(Asset, CareReminderType.Warranty30, Today, "CareProfile", Guid.NewGuid());
        reminder.Queue(Today, Now);
        reminder.MarkSent("Sms", Now);

        reminder.Cancel(Now);

        Assert.Equal(CareReminderStatus.Sent, reminder.Status);
    }

    [Fact]
    public void Snooze_ComesBackOnTheDayChosen()
    {
        var reminder = CareReminder.Schedule(Asset, CareReminderType.Warranty30, Today, "CareProfile", Guid.NewGuid());
        reminder.Snooze(Today.AddDays(7), Today);

        Assert.Equal((false, true), (reminder.Queue(Today.AddDays(6), Now), reminder.Queue(Today.AddDays(7), Now)));
    }

    [Fact]
    public void Snooze_ToToday_Throws()
    {
        var reminder = CareReminder.Schedule(Asset, CareReminderType.Warranty30, Today, "CareProfile", Guid.NewGuid());

        Assert.Throws<DomainException>(() => reminder.Snooze(Today, Today));
    }
}
