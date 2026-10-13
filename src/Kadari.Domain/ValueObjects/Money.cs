namespace Kadari.Domain.ValueObjects;

/// <summary>
/// A monetary amount in minor units (cents) plus an ISO-4217 currency (DB standards: no float for
/// money). Mapped as an EF owned type. Non-negative.
/// </summary>
public sealed record Money
{
    public Money(long amountMinor, string currency)
    {
        if (amountMinor < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(amountMinor), "Money cannot be negative.");
        }

        ArgumentException.ThrowIfNullOrWhiteSpace(currency);
        if (currency.Length != 3)
        {
            throw new ArgumentException("Currency must be an ISO-4217 alpha-3 code.", nameof(currency));
        }

        AmountMinor = amountMinor;
        Currency = currency.ToUpperInvariant();
    }

    public long AmountMinor { get; }
    public string Currency { get; }

    public static Money Zar(long amountMinor) => new(amountMinor, "ZAR");

    public override string ToString() => $"{Currency} {AmountMinor / 100m:0.00}";
}
