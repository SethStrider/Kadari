namespace Kadari.Application.Abstractions;

/// <summary>
/// Marks a placeholder implementation that must never serve production traffic (PAS-002 §3).
/// Stubs historically failed <em>open</em> — accepting any webhook signature, reporting every payment
/// as settled — so a single missing configuration value silently turned a public endpoint into an
/// unauthenticated entry point. The production readiness guard refuses to start the host when one of
/// these is bound outside Development.
/// </summary>
/// <remarks>
/// Only apply to placeholders awaiting a real provider. A legitimate always-on fallback (for example
/// the scripted conversation assistant, which backstops the AI on error) is <em>not</em> a stub.
/// </remarks>
public interface IDevelopmentOnlyStub;
