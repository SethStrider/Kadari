namespace Kadari.Application.Exceptions;

/// <summary>
/// The row was changed by a competing request between read and write — the optimistic concurrency
/// token (<c>xmin</c>, see CLAUDE.md §6) no longer matches. A lost update, not a server fault:
/// mapped to HTTP 409 at the API edge so the caller can re-read and retry.
/// </summary>
public sealed class ConcurrencyException(string message, Exception? innerException = null)
    : Exception(message, innerException);
