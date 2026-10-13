namespace Kadari.Application.Abstractions.Access;

/// <summary>
/// The request is deliberately <b>not</b> customer-scoped (PAS-002 §2) — public reads, the auth flow,
/// provider webhooks, platform-wide administration.
/// </summary>
/// <remarks>
/// This is an explicit, reviewable decision rather than an absence. Declaring it should come with a
/// one-line reason on the request type saying why no owner check applies; "it is a public catalogue
/// read" and "nobody got round to it" must not look the same in the code.
/// </remarks>
public interface IUnscopedRequest;
