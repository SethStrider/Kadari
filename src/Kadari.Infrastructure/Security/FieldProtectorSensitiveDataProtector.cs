using Kadari.Application.Abstractions.Security;

namespace Kadari.Infrastructure.Security;

/// <summary>The Application's protector over the process-wide <see cref="FieldProtector"/> (AES-256-GCM).</summary>
public sealed class FieldProtectorSensitiveDataProtector : ISensitiveDataProtector
{
    public string Protect(string plaintext) => FieldProtector.Encrypt(plaintext);

    public string Unprotect(string protectedText) => FieldProtector.Decrypt(protectedText);
}
