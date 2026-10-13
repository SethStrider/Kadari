namespace Kadari.Application.Common.Pagination;

/// <summary>
/// The upper bound for list queries that take a bare <c>take</c> rather than a full
/// <see cref="PageRequest"/> (PAS-002 §12).
/// </summary>
/// <remarks>
/// <para>
/// <see cref="PageRequest"/> has always clamped page size, but only one of the seven list queries used
/// it. The rest defaulted a non-positive <c>take</c> and then honoured whatever was passed — so
/// <c>?take=1000000</c> was an unbounded read a caller could ask for at will.
/// </para>
/// <para>
/// A lower-bound default is not a limit. This supplies the missing ceiling, sharing
/// <see cref="PageRequest.MaxPageSize"/> so both routes agree on what "too many" means.
/// </para>
/// </remarks>
public static class ResultLimit
{
    /// <summary>
    /// Clamps a caller-supplied <paramref name="take"/> into <c>[1, <see cref="PageRequest.MaxPageSize"/>]</c>,
    /// substituting <paramref name="fallback"/> when the caller supplied nothing meaningful.
    /// </summary>
    public static int Clamp(int take, int fallback = PageRequest.DefaultPageSize)
        => take <= 0
            ? Math.Clamp(fallback, 1, PageRequest.MaxPageSize)
            : Math.Clamp(take, 1, PageRequest.MaxPageSize);
}
