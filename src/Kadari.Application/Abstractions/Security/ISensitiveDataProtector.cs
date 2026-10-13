namespace Kadari.Application.Abstractions.Security;

/// <summary>
/// Encrypts text that carries personal data before it leaves the database's encrypted columns — e.g. a raw provider
/// payload archived to blob storage (GL-26). Same key and algorithm as the encrypted columns.
/// </summary>
public interface ISensitiveDataProtector
{
    string Protect(string plaintext);

    string Unprotect(string protectedText);
}
