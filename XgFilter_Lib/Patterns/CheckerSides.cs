namespace XgFilter_Lib.Patterns;

/// <summary>
/// A set of board sides: the on-roll player, the opponent, both, or neither.
/// The single owner of each side's signed value interval — the grammar-wide
/// sign rule, positive for the on-roll player and negative for the opponent —
/// and of which board entries belong to which side. A
/// <see cref="CheckerLocation"/> names the sides that can sit on it and takes
/// their hull as its interval; a <see cref="CheckerSpanRange"/> constrains
/// each side whose interval holds all its bounds.
/// </summary>
[Flags]
internal enum CheckerSides
{
    /// <summary>No side.</summary>
    None = 0,

    /// <summary>The on-roll player: positive board entries, values in <c>[0, 15]</c>.</summary>
    Player = 1,

    /// <summary>The opponent: negative board entries, values in <c>[-15, 0]</c>.</summary>
    Opponent = 2,

    /// <summary>Both sides: values in the hull <c>[-15, 15]</c>.</summary>
    Both = Player | Opponent,
}

/// <summary>
/// The interval, sign, and counting rules of <see cref="CheckerSides"/>.
/// </summary>
internal static class CheckerSidesExtensions
{
    /// <summary>
    /// Inclusive lower limit of the signed values <paramref name="sides"/> can
    /// exhibit: <c>-15</c> when the opponent is among them, else <c>0</c>.
    /// </summary>
    internal static int MinValue(this CheckerSides sides) =>
        (sides & CheckerSides.Opponent) != 0 ? -CheckerLocation.MaxCheckers : 0;

    /// <summary>
    /// Inclusive upper limit of the signed values <paramref name="sides"/> can
    /// exhibit: <c>15</c> when the on-roll player is among them, else <c>0</c>.
    /// </summary>
    internal static int MaxValue(this CheckerSides sides) =>
        (sides & CheckerSides.Player) != 0 ? CheckerLocation.MaxCheckers : 0;

    /// <summary>
    /// True when <paramref name="bound"/> is absent (unbounded) or lies within
    /// <paramref name="sides"/>' value interval.
    /// </summary>
    internal static bool Admits(this CheckerSides sides, int? bound) =>
        bound is not { } b || (sides.MinValue() <= b && b <= sides.MaxValue());

    /// <summary>
    /// Signs a non-negative checker <paramref name="count"/> for one
    /// <paramref name="side"/>: unchanged for the on-roll player, negated for
    /// the opponent.
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="side"/> is not exactly one side.
    /// </exception>
    internal static int Signed(this CheckerSides side, int count) => side switch
    {
        CheckerSides.Player => count,
        CheckerSides.Opponent => -count,
        _ => throw new ArgumentOutOfRangeException(nameof(side), side, "Expected exactly one side."),
    };

    /// <summary>
    /// Counts <paramref name="side"/>'s checkers, as a non-negative number, on
    /// board entries <paramref name="start"/> (inclusive) to
    /// <paramref name="end"/> (exclusive). The other side's entries in that
    /// run are ignored, never netted off.
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="side"/> is not exactly one side.
    /// </exception>
    internal static int CheckersOn(this CheckerSides side, IReadOnlyList<int> board, int start, int end)
    {
        int sign = side.Signed(1);
        int count = 0;
        for (int i = start; i < end; i++)
        {
            int signed = board[i] * sign;
            if (signed > 0)
                count += signed;
        }
        return count;
    }
}
