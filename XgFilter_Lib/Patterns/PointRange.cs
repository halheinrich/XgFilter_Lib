namespace XgFilter_Lib.Patterns;

/// <summary>
/// An inclusive signed-count constraint on a single <see cref="PatternSlot"/>,
/// the element of a <see cref="BoardPattern"/>. <see cref="Slot"/> names the
/// location — a board-array index 0–25 (bars included) or one side's borne-off
/// count; <see cref="Min"/>/<see cref="Max"/> are inclusive bounds on the
/// on-roll-relative checker count there, where negative counts are the
/// opponent's checkers and <see langword="null"/> means that side is
/// unbounded. (The name predates the borne-off slots — every constrainable
/// location was a board index; "point" now reads loosely as "location".)
///
/// <para>
/// A <see cref="PointRange"/> is validated at construction and is therefore
/// never invalid once it exists — <see cref="BoardPattern"/> can rely on every
/// element being well-formed and need only police the cross-element invariant
/// (no duplicate <see cref="Slot"/>). Bounds follow the grammar-wide sign
/// rule (positive = on-roll player, negative = opponent) and must lie within
/// the slot's own value interval: <c>[-15, 15]</c> for board slots,
/// <c>[0, 15]</c> for <see cref="PatternSlot.PlayerOff"/>, <c>[-15, 0]</c> for
/// <see cref="PatternSlot.OpponentOff"/> — so a wrong-signed borne-off bound
/// is a construction error, not a constraint that silently never matches. The
/// 15 ceiling is <see cref="PatternSlot.MaxCheckers"/>.
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
    /// <summary>The slot this range constrains.</summary>
    public PatternSlot Slot { get; }

    /// <summary>
    /// Inclusive lower bound on the signed checker count at <see cref="Slot"/>;
    /// <see langword="null"/> leaves the lower side unbounded.
    /// </summary>
    public int? Min { get; }

    /// <summary>
    /// Inclusive upper bound on the signed checker count at <see cref="Slot"/>;
    /// <see langword="null"/> leaves the upper side unbounded.
    /// </summary>
    public int? Max { get; }

    /// <summary>
    /// Creates a validated constraint on board-array index
    /// <paramref name="index"/> — the convenience form of
    /// <see cref="PointRange(PatternSlot, int?, int?)"/> for the common
    /// board-slot case.
    /// </summary>
    /// <param name="index">Board-array index, 0–25.</param>
    /// <param name="min">Inclusive lower bound, or <see langword="null"/>.</param>
    /// <param name="max">Inclusive upper bound, or <see langword="null"/>.</param>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="index"/> is outside 0–25, or a bound's magnitude exceeds
    /// <see cref="PatternSlot.MaxCheckers"/>.
    /// </exception>
    /// <exception cref="ArgumentException">
    /// <paramref name="min"/> is greater than <paramref name="max"/> (an
    /// empty range that could never match).
    /// </exception>
    public PointRange(int index, int? min, int? max)
        : this(PatternSlot.Board(index), min, max)
    {
    }

    /// <summary>
    /// Creates a validated constraint on <paramref name="slot"/>. Bounds are
    /// inclusive and signed; pass <see langword="null"/> to leave a side
    /// unbounded.
    /// </summary>
    /// <param name="slot">The slot to constrain.</param>
    /// <param name="min">Inclusive lower bound, or <see langword="null"/>.</param>
    /// <param name="max">Inclusive upper bound, or <see langword="null"/>.</param>
    /// <exception cref="ArgumentOutOfRangeException">
    /// A bound lies outside the slot's value interval — beyond
    /// ±<see cref="PatternSlot.MaxCheckers"/>, or wrong-signed for a borne-off
    /// slot (<see cref="PatternSlot.PlayerOff"/> admits only <c>[0, 15]</c>,
    /// <see cref="PatternSlot.OpponentOff"/> only <c>[-15, 0]</c>).
    /// </exception>
    /// <exception cref="ArgumentException">
    /// <paramref name="min"/> is greater than <paramref name="max"/> (an
    /// empty range that could never match).
    /// </exception>
    public PointRange(PatternSlot slot, int? min, int? max)
    {
        if (min is { } lo && (lo < slot.MinValue || lo > slot.MaxValue))
            throw new ArgumentOutOfRangeException(
                nameof(min), min,
                $"Bound must be within [{slot.MinValue}, {slot.MaxValue}] for slot '{slot}'.");

        if (max is { } hi && (hi < slot.MinValue || hi > slot.MaxValue))
            throw new ArgumentOutOfRangeException(
                nameof(max), max,
                $"Bound must be within [{slot.MinValue}, {slot.MaxValue}] for slot '{slot}'.");

        if (min is { } l && max is { } h && l > h)
            throw new ArgumentException(
                $"Min ({l}) must not exceed Max ({h}) for slot '{slot}'.", nameof(min));

        Slot = slot;
        Min = min;
        Max = max;
    }

    /// <summary>
    /// Tests whether <paramref name="value"/>, the signed checker count at
    /// <see cref="Slot"/>, satisfies this range. An unbounded side admits
    /// everything on that side.
    /// </summary>
    public bool Contains(int value) =>
        (Min ?? int.MinValue) <= value && value <= (Max ?? int.MaxValue);

    /// <summary>
    /// Tests whether <paramref name="board"/> satisfies this constraint: the
    /// slot's value on the board — read directly for a board slot, derived for
    /// a borne-off slot — checked against the range.
    /// </summary>
    internal bool IsSatisfiedBy(IReadOnlyList<int> board) => Contains(Slot.ValueOn(board));

    /// <summary>
    /// Renders this range in the <c>[slot,min,max]</c> bracket-token form used
    /// by <see cref="BoardPattern.Parse"/> — the slot head is the bare index or
    /// a named token (<c>off</c> / <c>opp-off</c>); an unbounded side is
    /// written as an empty field. Round-trips through
    /// <see cref="BoardPattern.Parse"/>.
    /// </summary>
    public override string ToString() => $"[{Slot},{Min},{Max}]";
}
