namespace Kadari.Application.Features.Identity.Services;

/// <summary>
/// The server-side pepper for identity-key hashing. A national ID is low-entropy and enumerable, so the key
/// is an HMAC under this secret — without it, a leaked identity key cannot be brute-forced back to the ID.
/// Set per environment (a real secret in production); it defaults empty for local/dev/test, which still
/// produces consistent keys within that environment.
/// </summary>
public sealed class IdentityHashOptions
{
    public const string SectionName = "IdentityHash";

    public string Secret { get; set; } = string.Empty;
}
