namespace Kadari.Application.Abstractions.Messaging;

/// <summary>
/// Marks an exception that rejects the operation but whose side-effects committed so far must still be
/// persisted (the transaction commits, then rethrows) — e.g. a failed OTP attempt must increment the
/// brute-force counter even though verification is rejected. Use sparingly and deliberately: a plain
/// exception rolls the whole transaction back.
/// </summary>
public interface IPersistableRejection;
