using Kadari.Domain.Abstractions;
using Kadari.Domain.Care;
using Kadari.Domain.Tests.CareRegistry;

namespace Kadari.Domain.Tests.Care;

public class OpsWorkItemTests
{
    private static readonly DateTimeOffset Now = RuleFactory.Now;
    private static readonly Guid Operator = Guid.Parse("0192f000-0000-7000-8000-0000000000e1");

    private static OpsWorkItem Raise(OpsSeverity severity = OpsSeverity.P2) =>
        OpsWorkItem.Raise(OpsWorkType.RouteUnknown, severity, null, null, Guid.Parse("0192f000-0000-7000-8000-0000000000d1"),
            "no-verified-route", "Find the route.", Now);

    [Fact]
    public void Raise_SafetyIsDueWithinTheHour()
    {
        Assert.Equal(Now.AddHours(1), Raise(OpsSeverity.P0).DueAt);
    }

    [Fact]
    public void Raise_TheSameExceptionHasTheSameKey()
    {
        Assert.Equal(Raise().DedupeKey, Raise().DedupeKey);
    }

    [Fact]
    public void Take_AssignsAndStartsIt()
    {
        var item = Raise();

        item.Take(Operator);

        Assert.Equal((OpsWorkStatus.InProgress, (Guid?)Operator), (item.Status, item.AssignedTo));
    }

    [Fact]
    public void Resolve_RecordsTheKindOfKnowledgeAndWho()
    {
        var item = Raise();

        item.Resolve(Operator, OpsResolution.ReusableKnowledge, "Hisense SA routes small appliances to the retailer.", Now);

        Assert.Equal((OpsWorkStatus.Resolved, (OpsResolution?)OpsResolution.ReusableKnowledge, (Guid?)Operator),
            (item.Status, item.Resolution, item.ResolvedBy));
    }

    [Fact]
    public void Resolve_Twice_IsRefused()
    {
        var item = Raise();
        item.Resolve(Operator, OpsResolution.CaseSpecific, "Done", Now);

        Assert.Throws<DomainException>(() => item.Resolve(Operator, OpsResolution.CaseSpecific, "Again", Now));
    }

    [Fact]
    public void Close_OnAResolvedItem_ChangesNothing()
    {
        var item = Raise();
        item.Resolve(Operator, OpsResolution.ProductBug, "Bug", Now);

        item.Close("The case closed.", Now);

        Assert.Equal(OpsResolution.ProductBug, item.Resolution);
    }
}
