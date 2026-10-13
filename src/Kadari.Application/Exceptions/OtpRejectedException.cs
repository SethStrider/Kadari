using Kadari.Application.Abstractions.Messaging;

namespace Kadari.Application.Exceptions;

/// <summary>
/// OTP verification was rejected (wrong/expired code, too many attempts). A 403 like its base, but marked
/// <see cref="IPersistableRejection"/> so the failed-attempt increment (brute-force protection) is
/// committed before the rejection surfaces, rather than rolled back with the transaction.
/// </summary>
public sealed class OtpRejectedException(string message) : ForbiddenException(message), IPersistableRejection;
