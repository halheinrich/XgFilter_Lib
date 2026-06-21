namespace XgFilter_Lib.Patterns;

/// <summary>
/// An inclusive signed-count constraint on a single board-array index, the
/// element of a <see cref="BoardPattern"/>. <see cref="Index"/> is a
/// board-array index 0–25 (bars included); <see cref="Min"/>/<see cref="Max"/>
/// are inclusive bounds on the on-roll-relative checker count at that index,
/// where negative counts are the opponent's checkers and <see langword="null"/>
/// means that side is unbounded.
///
/// <para>
/// A <see cref="PointRange"/> is validated at construction and is therefore
/// never invalid once it exists — <see cref="BoardPattern"/> can rely on every
/// element being well-formed and need only police the cross-element invariant
/// (no duplicate <see cref="Index"/>). The bounds are the on-roll-relative
/// board convention: <c>0</c> = opponent's bar, <c>1..24</c> = points,
/// <c>25</c> = on-roll player's bar; positive = on-roll player's checkers,
/// negative = opponent's. The <c>±15</c> bound on the magnitudes is the
/// fifteen-checkers-per-side ceiling.
/// </para>
///
/// <para>
/// Declared a <see langword="readonly record struct"/> so it is immutable and
/// carries structural value-equality for free — the equality footgun that
/// makes <see cref="BoardPattern"/> decline value-equality is the mutable,
/// reference-typed backing list it wraps, not this small immutable element.
/// </para>
/// </summary>
public readonly record struct PointRange
{
    /// <summary>Largest checker count a single index may bound, per side.</summary>
    public const int MaxCheckers = 15;

    /// <summary>Highest valid board-array index (the on-roll player's bar).</summary>
    public const int MaxIndex = 25;

    /// <summary>The board-array index this range constrains (0–25, bars included).</summary>
    public int Index { get; }

    /// <summary>
    /// Inclusive lower bound on the signed checker count at <see cref="Index"/>;
    /// <see langword="null"/> leaves the lower side unbounded.
    /// </summary>
    public int? Min { get; }

    /// <summary>
    /// Inclusive upper bound on the signed checker count at <see cref="Index"/>;
    /// <see langword="null"/> leaves the upper side unbounded.
    /// </summary>
    public int? Max { get; }

    /// <summary>
    /// Creates a validated constraint on <paramref name="index"/>. Bounds are
    /// inclusive and signed; pass <see langword="null"/> to leave a side
    /// unbounded.
    /// </summary>
    /// <param name="index">Board-array index, 0–25.</param>
    /// <param name="min">Inclusive lower bound, or <see langword="null"/>.</param>
    /// <param name="max">Inclusive upper bound, or <see langword="null"/>.</param>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="index"/> is outside 0–25, or a bound's magnitude exceeds
    /// <see cref="MaxCheckers"/>.
    /// </exception>
    /// <exception cref="ArgumentException">
    /// <paramref name="min"/> is greater than <paramref name="max"/> (an
    /// empty range that could never match).
    /// </exception>
    public PointRange(int index, int? min, int? max)
    {
        if (index is < 0 or > MaxIndex)
            throw new ArgumentOutOfRangeException(
                nameof(index), index, $"Board index must be in [0, {MaxIndex}].");

        if (min is { } lo && Math.Abs(lo) > MaxCheckers)
            throw new ArgumentOutOfRangeException(
                nameof(min), min, $"Bound magnitude must not exceed {MaxCheckers}.");

        if (max is { } hi && Math.Abs(hi) > MaxCheckers)
            throw new ArgumentOutOfRangeException(
                nameof(max), max, $"Bound magnitude must not exceed {MaxCheckers}.");

        if (min is { } l && max is { } h && l > h)
            throw new ArgumentException(
                $"Min ({l}) must not exceed Max ({h}) for index {index}.", nameof(min));

        Index = index;
        Min = min;
        Max = max;
    }

    /// <summary>
    /// Tests whether <paramref name="value"/>, the signed checker count at
    /// <see cref="Index"/>, satisfies this range. An unbounded side admits
    /// everything on that side.
    /// </summary>
    public bool Contains(int value) =>
        (Min ?? int.MinValue) <= value && value <= (Max ?? int.MaxValue);

    /// <summary>
    /// Renders this range in the <c>[index,min,max]</c> bracket-token form used
    /// by <see cref="BoardPattern.Parse"/>; an unbounded side is written as an
    /// empty field. Round-trips through <see cref="BoardPattern.Parse"/>.
    /// </summary>
    public override string ToString() => $"[{Index},{Min},{Max}]";
}
