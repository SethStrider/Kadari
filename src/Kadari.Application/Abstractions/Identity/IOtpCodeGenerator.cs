namespace Kadari.Application.Abstractions.Identity;

/// <summary>Generates OTP codes and hashes them for storage (codes are never stored in the clear).</summary>
public interface IOtpCodeGenerator
{
    string Generate();

    string Hash(string code);
}
