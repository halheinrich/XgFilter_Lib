namespace XgFilter_Lib.Patterns;

/// <summary>
/// An inclusive bound on one side's <em>total</em> checkers across a
/// <see cref="CheckerSpan"/> — the span constraint of a
/// <see cref="BoardPattern"/>, written <c>[a-b,min,max]</c> in the bracket
/// grammar.
///
/// <para>
/// Let P be the on-roll player's checkers summed over the span and O the
/// opponent's, as a non-negative count. The sign of the bounds names the side,
/// by the grammar-wide sign rule:
/// </para>
/// <list type="bullet">
/// <item><description>
/// A positive bound: the on-roll player, <c>min ≤ P ≤ max</c>.
/// </description></item>
/// <item><description>
/// A negative bound: the opponent, <c>min ≤ −O ≤ max</c> — so
/// <c>[13-18,,-2]</c> means the opponent has two or more there.
/// </description></item>
/// <item><description>
/// One positive and one negative bound: refused at construction.
/// </description></item>
/// <item><description>
/// Bounds that are all zero or absent read on both sides, where the side that
/// could only be bounded trivially drops out: <c>[a-b,0,0]</c> means the span is
/// empty of both sides, <c>[a-b,,0]</c> that the on-roll player has none there,
/// <c>[a-b,0,]</c> that the opponent has none there, and <c>[a-b,,]</c> nothing.
/// </description></item>
/// </list>
/// <para>
/// Formally: the constraint applies its bounds to every side whose value
/// interval (<c>[0, 15]</c> for the on-roll player, <c>[-15, 0]</c> for the
/// opponent) holds all of them, and a pair no side's interval holds is
/// refused. <b>The other side's checkers in the span are ignored</b>: the
/// count is never netted, so adding opposing checkers never changes a signed
/// span's verdict. This is where a span differs from a
/// <see cref="CheckerRange"/> on one point, whose signed count is the whole of
/// that point and so does see the opposing side — <c>[6,0,3]</c> rejects an
/// opponent's blot on the 6-point, <c>[6-7,0,3]</c> does not. A span may
/// include either bar with either sign; a bar adds nothing to the side that
/// cannot sit on it.
/// </para>
///
/// <para>
/// Validated at construction and never invalid once it exists — the posture of
/// <see cref="CheckerRange"/>. It holds no state beyond its span and bounds (the
/// constrained sides are derived from the bounds when read), so
/// <c>default(CheckerSpanRange)</c> is the valid, unconstraining <c>[0-1,,]</c>.
/// A <see langword="readonly record struct"/> for immutability and structural
/// equality, which <see cref="BoardPattern"/>'s equality delegates to.
/// </para>
/// </summary>
public readonly record struct CheckerSpanRange : IPatternConstraint
{
    /// <summary>The run of board indices this constraint totals over.</summary>
    public CheckerSpan Span { get; }

    /// <summary>
    /// Inclusive lower bound on the constrained side's signed total, or
    /// <see langword="null"/> for none.
    /// </summary>
    public int? Min { get; }

    /// <summary>
    /// Inclusive upper bound on the constrained side's signed total, or
    /// <see langword="null"/> for none.
    /// </summary>
    public int? Max { get; }

    /// <summary>
    /// Creates a validated constraint on the span of board-array indices
    /// <paramref name="first"/> to <paramref name="last"/> — the convenience
    /// form of <see cref="CheckerSpanRange(CheckerSpan, int?, int?)"/>.
    /// </summary>
    /// <param name="first">The lower board-array index.</param>
    /// <param name="last">The higher board-array index.</param>
    /// <param name="min">Inclusive lower bound, or <see langword="null"/>.</param>
    /// <param name="max">Inclusive upper bound, or <see langword="null"/>.</param>
    /// <exception cref="ArgumentOutOfRangeException">
    /// An index is outside 0–25, or a bound's magnitude exceeds
    /// <see cref="CheckerLocation.MaxCheckers"/>.
    /// </exception>
    /// <exception cref="ArgumentException">
    /// <paramref name="first"/> is not less than <paramref name="last"/>, the
    /// bounds have opposite signs, or <paramref name="min"/> exceeds
    /// <paramref name="max"/>.
    /// </exception>
    public CheckerSpanRange(int first, int last, int? min, int? max)
        : this(new CheckerSpan(first, last), min, max)
    {
    }

    /// <summary>
    /// Creates a validated constraint on <paramref name="span"/>. Bounds are
    /// inclusive and signed, and their sign names the side constrained; pass
    /// <see langword="null"/> to leave a side unbounded.
    /// </summary>
    /// <param name="span">The run of board indices to total over.</param>
    /// <param name="min">Inclusive lower bound, or <see langword="null"/>.</param>
    /// <param name="max">Inclusive upper bound, or <see langword="null"/>.</param>
    /// <exception cref="ArgumentOutOfRangeException">
    /// A bound's magnitude exceeds <see cref="CheckerLocation.MaxCheckers"/>.
    /// </exception>
    /// <exception cref="ArgumentException">
    /// One bound is positive and the other negative (a span constrains one
    /// side), or <paramref name="min"/> exceeds <paramref name="max"/>.
    /// </exception>
    public CheckerSpanRange(CheckerSpan span, int? min, int? max)
    {
        ThrowIfOutsideBothSides(min, nameof(min), span);
        ThrowIfOutsideBothSides(max, nameof(max), span);

        if (SidesAdmitting(min, max) == CheckerSides.None)
            throw new ArgumentException(
                $"Bounds ({min}, {max}) for span '{span}' have opposite signs; a span " +
                "constrains one side, named by the sign of its bounds.", nameof(min));

        InclusiveBounds.ThrowIfMinExceedsMax(min, max, span);

        Span = span;
        Min = min;
        Max = max;
    }

    /// <summary>
    /// The sides this constraint bounds: every side whose value interval holds
    /// both bounds. Never <see cref="CheckerSides.None"/> once constructed.
    /// </summary>
    internal CheckerSides ConstrainedSides => SidesAdmitting(Min, Max);

    /// <inheritdoc/>
    object IPatternConstraint.Place => Span;

    /// <summary>
    /// True when each constrained side's signed total across the span lies
    /// within the bounds. Each side is counted alone; the other side's
    /// checkers in the span are ignored.
    /// </summary>
    bool IPatternConstraint.IsSatisfiedBy(IReadOnlyList<int> board)
    {
        var sides = ConstrainedSides;
        foreach (var side in (ReadOnlySpan<CheckerSides>)[CheckerSides.Player, CheckerSides.Opponent])
        {
            if ((sides & side) != 0
                && !InclusiveBounds.Contain(Min, Max, side.Signed(Span.CheckersOn(side, board))))
                return false;
        }
        return true;
    }

    /// <summary>
    /// Renders this constraint in the <c>[a-b,min,max]</c> bracket-token form
    /// used by <see cref="BoardPattern.Parse"/>; an unbounded side is written as
    /// an empty field. Round-trips through <see cref="BoardPattern.Parse"/>.
    /// </summary>
    public override string ToString() => $"[{Span},{Min},{Max}]";

    private static CheckerSides SidesAdmitting(int? min, int? max)
    {
        var sides = CheckerSides.None;
        if (CheckerSides.Player.Admits(min) && CheckerSides.Player.Admits(max))
            sides |= CheckerSides.Player;
        if (CheckerSides.Opponent.Admits(min) && CheckerSides.Opponent.Admits(max))
            sides |= CheckerSides.Opponent;
        return sides;
    }

    private static void ThrowIfOutsideBothSides(int? bound, string paramName, CheckerSpan span)
    {
        if (!CheckerSides.Both.Admits(bound))
            throw new ArgumentOutOfRangeException(
                paramName, bound,
                $"Bound must be within [{CheckerSides.Both.MinValue()}, {CheckerSides.Both.MaxValue()}] " +
                $"for span '{span}'.");
    }
}
