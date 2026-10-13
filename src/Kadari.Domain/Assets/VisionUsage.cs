using Kadari.Domain.Abstractions;
using Kadari.Domain.Customers;

namespace Kadari.Domain.Assets;

public readonly record struct VisionUsageId(Guid Value)
{
    public static VisionUsageId New() => new(Guid.CreateVersion7());
}

/// <summary>
/// How many paid vision-model calls one customer's captures made on one UTC day (AS-09) — the counter the daily
/// quota reads. One row per (customer, day); each metered call adds one, whether the model succeeded or not,
/// because a failed call is still billed.
/// </summary>
public sealed class VisionUsage : AggregateRoot<VisionUsageId>
{
    private VisionUsage() { }   // EF

    private VisionUsage(VisionUsageId id, CustomerId customerId, DateOnly day) : base(id)
    {
        CustomerId = customerId;
        Day = day;
    }

    public CustomerId CustomerId { get; private set; }
    public DateOnly Day { get; private set; }
    public int Calls { get; private set; }

    public static VisionUsage Start(CustomerId customerId, DateOnly day) => new(VisionUsageId.New(), customerId, day);

    public void RecordCall() => Calls++;
}
