namespace XgFilter_Lib.Patterns;

/// <summary>
/// The one reading of a constraint's optional inclusive bound pair, shared by
/// <see cref="CheckerRange"/> and <see cref="CheckerSpanRange"/>: a
/// <see langword="null"/> bound leaves that side unbounded.
/// </summary>
internal static class InclusiveBounds
{
    /// <summary>
    /// True when <paramref name="value"/> lies within
    /// <c>[min, max]</c>, an absent bound admitting everything on its side.
    /// </summary>
    internal static bool Contain(int? min, int? max, int value) =>
        (min ?? int.MinValue) <= value && value <= (max ?? int.MaxValue);

    /// <summary>
    /// Refuses a bound pair that no value could satisfy.
    /// </summary>
    /// <param name="min">The lower bound, or <see langword="null"/>.</param>
    /// <param name="max">The upper bound, or <see langword="null"/>.</param>
    /// <param name="place">The constrained place, for the message.</param>
    /// <exception cref="ArgumentException"><paramref name="min"/> exceeds <paramref name="max"/>.</exception>
    internal static void ThrowIfMinExceedsMax(int? min, int? max, object place)
    {
        if (min is { } l && max is { } h && l > h)
            throw new ArgumentException(
                $"Min ({l}) must not exceed Max ({h}) for '{place}'.", nameof(min));
    }
}
