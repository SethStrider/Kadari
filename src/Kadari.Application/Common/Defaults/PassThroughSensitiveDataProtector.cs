using Kadari.Application.Abstractions;
using Kadari.Application.Abstractions.Security;

namespace Kadari.Application.Common;

/// <summary>
/// Default <see cref="ISensitiveDataProtector"/> registered by <c>AddApplication</c>: leaves text as it is, so the
/// Application layer runs without Infrastructure (unit tests, in-memory hosts). Infrastructure registers the real
/// AES-GCM protector, which wins in the composed app — and because this is a development-only stub, the production
/// readiness guard refuses to start a host where it somehow survived (GL-26).
/// </summary>
internal sealed class PassThroughSensitiveDataProtector : ISensitiveDataProtector, IDevelopmentOnlyStub
{
    public string Protect(string plaintext) => plaintext;

    public string Unprotect(string protectedText) => protectedText;
}
