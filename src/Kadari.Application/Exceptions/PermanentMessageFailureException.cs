namespace Kadari.Application.Exceptions;

/// <summary>
/// A message cannot succeed however often it is retried — an external party refused it for good (a 4xx that is not
/// a rate limit), or its data is wrong (GL-29). The bus sends it straight to the error queue for a person to look
/// at, instead of burning retries and redeliveries on it.
/// </summary>
public sealed class PermanentMessageFailureException(string message, Exception? inner = null) : Exception(message, inner);
