using Kadari.Application.Abstractions.Messaging;

namespace Kadari.Application.Exceptions;

/// <summary>
/// A password sign-in was refused — wrong password, unknown email, an account without a password, one
/// that lacks the staff standing the portal requires, or one under a brute-force lockout. Every one of
/// these answers with the <em>same</em> generic message on purpose: a response that distinguishes them
/// tells an attacker which emails are staff accounts.
/// <para>
/// Marked <see cref="IPersistableRejection"/> so a genuine account's failed-attempt increment (the
/// brute-force counter) is committed before the rejection surfaces, rather than rolled back with the
/// transaction — the same mechanism <c>OtpRejectedException</c> relies on. Mapped to HTTP 401.
/// </para>
/// </summary>
public sealed class InvalidCredentialsException()
    : Exception("Invalid email or password."), IPersistableRejection;
