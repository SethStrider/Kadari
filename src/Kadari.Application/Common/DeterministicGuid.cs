using System.Security.Cryptography;
using System.Text;

namespace Kadari.Application.Common;

/// <summary>
/// Derives a stable <see cref="Guid"/> from a string, so the same input always yields the same id.
/// Used to give an integration event a deterministic message id (e.g. from a WhatsApp <c>wamid</c>),
/// which the MassTransit inbox uses to de-duplicate provider retries.
/// </summary>
public static class DeterministicGuid
{
    public static Guid From(string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);
        var hash = MD5.HashData(Encoding.UTF8.GetBytes(value));   // 16 bytes → a Guid; collision-free enough for ids
        return new Guid(hash);
    }
}
