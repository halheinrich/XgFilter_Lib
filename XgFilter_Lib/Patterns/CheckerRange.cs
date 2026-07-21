namespace XgFilter_Lib.Patterns;

/// <summary>
/// An inclusive signed-count constraint on a single <see cref="CheckerLocation"/>,
/// the element of a <see cref="BoardPattern"/>. <see cref="Location"/> names the
/// place — a board-array index 0–25 (bars included) or one side's borne-off
/// count; <see cref="Min"/>/<see cref="Max"/> are inclusive bounds on the
/// on-roll-relative checker count there, where negative counts are the
/// opponent's checkers and <see langword="null"/> means that side is
/// unbounded.
///
/// <para>
/// A <see cref="CheckerRange"/> is validated at construction and is therefore
/// never invalid once it exists — <see cref="BoardPattern"/> can rely on every
/// element being well-formed and need only police the cross-element invariant
/// (no duplicate <see cref="Location"/>). Bounds follow the grammar-wide sign
/// rule (positive = on-roll player, negative = opponent) and must lie within
/// the location's own value interval: <c>[-15, 15]</c> for board locations,
/// <c>[0, 15]</c> for <see cref="CheckerLocation.PlayerOff"/>, <c>[-15, 0]</c> for
/// <see cref="CheckerLocation.OpponentOff"/> — so a wrong-signed borne-off bound
/// is a construction error, not a constraint that silently never matches. The
/// 15 ceiling is <see cref="CheckerLocation.MaxCheckers"/>.
/// </para>
///
/// <para>
/// Declared a <see langword="readonly record struct"/> so it is immutable and
/// carries structural value-equality for free — the equality footgun that
/// makes <see cref="BoardPattern"/> decline value-equality is the mutable,
/// reference-typed backing list it wraps, not this small immutable element.
/// </para>
/// </summary>
public readonly record struct CheckerRange
{
    /// <summary>The location this range constrains.</summary>
    public CheckerLocation Location { get; }

    /// <summary>
    /// Inclusive lower bound on the signed checker count at <see cref="Location"/>;
    /// <see langword="null"/> leaves the lower side unbounded.
    /// </summary>
    public int? Min { get; }

    /// <summary>
    /// Inclusive upper bound on the signed checker count at <see cref="Location"/>;
    /// <see langword="null"/> leaves the upper side unbounded.
    /// </summary>
    public int? Max { get; }

    /// <summary>
    /// Creates a validated constraint on board-array index
    /// <paramref name="index"/> — the convenience form of
    /// <see cref="CheckerRange(CheckerLocation, int?, int?)"/> for the common
    /// board-location case.
    /// </summary>
    /// <param name="index">Board-array index, 0–25.</param>
    /// <param name="min">Inclusive lower bound, or <see langword="null"/>.</param>
    /// <param name="max">Inclusive upper bound, or <see langword="null"/>.</param>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="index"/> is outside 0–25, or a bound's magnitude exceeds
    /// <see cref="CheckerLocation.MaxCheckers"/>.
    /// </exception>
    /// <exception cref="ArgumentException">
    /// <paramref name="min"/> is greater than <paramref name="max"/> (an
    /// empty range that could never match).
    /// </exception>
    public CheckerRange(int index, int? min, int? max)
        : this(CheckerLocation.Board(index), min, max)
    {
    }

    /// <summary>
    /// Creates a validated constraint on <paramref name="location"/>. Bounds are
    /// inclusive and signed; pass <see langword="null"/> to leave a side
    /// unbounded.
    /// </summary>
    /// <param name="location">The location to constrain.</param>
    /// <param name="min">Inclusive lower bound, or <see langword="null"/>.</param>
    /// <param name="max">Inclusive upper bound, or <see langword="null"/>.</param>
    /// <exception cref="ArgumentOutOfRangeException">
    /// A bound lies outside the location's value interval — beyond
    /// ±<see cref="CheckerLocation.MaxCheckers"/>, or wrong-signed for a borne-off
    /// count (<see cref="CheckerLocation.PlayerOff"/> admits only <c>[0, 15]</c>,
    /// <see cref="CheckerLocation.OpponentOff"/> only <c>[-15, 0]</c>).
    /// </exception>
    /// <exception cref="ArgumentException">
    /// <paramref name="min"/> is greater than <paramref name="max"/> (an
    /// empty range that could never match).
    /// </exception>
    public CheckerRange(CheckerLocation location, int? min, int? max)
    {
        if (min is { } lo && (lo < location.MinValue || lo > location.MaxValue))
            throw new ArgumentOutOfRangeException(
                nameof(min), min,
                $"Bound must be within [{location.MinValue}, {location.MaxValue}] for location '{location}'.");

        if (max is { } hi && (hi < location.MinValue || hi > location.MaxValue))
            throw new ArgumentOutOfRangeException(
                nameof(max), max,
                $"Bound must be within [{location.MinValue}, {location.MaxValue}] for location '{location}'.");

        if (min is { } l && max is { } h && l > h)
            throw new ArgumentException(
                $"Min ({l}) must not exceed Max ({h}) for location '{location}'.", nameof(min));

        Location = location;
        Min = min;
        Max = max;
    }

    /// <summary>
    /// Tests whether <paramref name="value"/>, the signed checker count at
    /// <see cref="Location"/>, satisfies this range. An unbounded side admits
    /// everything on that side.
    /// </summary>
    public bool Contains(int value) =>
        (Min ?? int.MinValue) <= value && value <= (Max ?? int.MaxValue);

    /// <summary>
    /// Tests whether <paramref name="board"/> satisfies this constraint: the
    /// location's value on the board — read directly for a board location,
    /// derived for a borne-off count — checked against the range.
    /// </summary>
    internal bool IsSatisfiedBy(IReadOnlyList<int> board) => Contains(Location.ValueOn(board));

    /// <summary>
    /// Renders this range in the <c>[location,min,max]</c> bracket-token form
    /// used by <see cref="BoardPattern.Parse"/> — the location head is the bare
    /// index or a named token (<c>off</c> / <c>opp-off</c>); an unbounded side is
    /// written as an empty field. Round-trips through
    /// <see cref="BoardPattern.Parse"/>.
    /// </summary>
    public override string ToString() => $"[{Location},{Min},{Max}]";
}
