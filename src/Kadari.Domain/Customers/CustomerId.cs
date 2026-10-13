namespace Kadari.Domain.Customers;

/// <summary>Strongly-typed key for <see cref="Customer"/>. UUIDv7, assigned at construction.</summary>
public readonly record struct CustomerId(Guid Value)
{
    public static CustomerId New() => new(Guid.CreateVersion7());
}
