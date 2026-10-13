namespace Kadari.WebApi.Configuration;

/// <summary>
/// The shared secret the Product Engine (n8n) signs report-intake pushes with (GL-05). Unset means the push
/// endpoint does not exist (404) — the safe launch default until n8n is configured with the same key.
/// </summary>
public sealed class ReportIntakeOptions
{
    public const string SectionName = "ReportIntake";

    /// <summary>HMAC-SHA256 key, ≥ 32 bytes when set.</summary>
    public string SigningKey { get; init; } = string.Empty;

    /// <summary>How far a signature's timestamp may drift from now before it is treated as a replay.</summary>
    public int ToleranceSeconds { get; init; } = 300;
}
