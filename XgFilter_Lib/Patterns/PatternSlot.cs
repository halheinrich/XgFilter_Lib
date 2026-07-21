using System.Diagnostics;
using System.Globalization;

namespace XgFilter_Lib.Patterns;

/// <summary>
/// The kind of location a <see cref="PatternSlot"/> addresses: one of the 26
/// board-array indices, or one side's borne-off checker count.
/// </summary>
public enum PatternSlotKind
{
    /// <summary>A board-array index 0–25 (bars included).</summary>
    Board,

    /// <summary>
    /// The on-roll player's borne-off checker count, a derived non-negative
    /// value (see <see cref="PatternSlot.PlayerOff"/>).
    /// </summary>
    PlayerOff,

    /// <summary>
    /// The opponent's borne-off checker count, a derived non-positive value
    /// (see <see cref="PatternSlot.OpponentOff"/>).
    /// </summary>
    OpponentOff,
}

/// <summary>
/// The location a <see cref="PointRange"/> constrains — either one of the 26
/// on-roll-relative board-array indices (<see cref="PatternSlotKind.Board"/>)
/// or one side's borne-off checker count, a value <em>derived</em> from the
/// board rather than stored in it. This type is the single source of truth for
/// slot vocabulary: which slots exist, the token names the bracket grammar
/// uses for the named ones (<c>off</c>, <c>opp-off</c>), the interval of
/// signed counts each slot can exhibit, and how each slot's value is read (or
/// derived) from a board.
///
/// <para>
/// Signed convention — one rule across the whole grammar: positive counts are
/// the on-roll player's, negative the opponent's. The borne-off slots follow
/// it: <see cref="PlayerOff"/> takes values in <c>[0, 15]</c> and
/// <see cref="OpponentOff"/> in <c>[-15, 0]</c> (e.g. a value of <c>-2</c>
/// means the opponent has two checkers off). Off counts are derived as
/// fifteen minus the side's on-board sum, bars included — the board array
/// carries no off slot of its own.
/// </para>
///
/// <para>
/// Constructed only through <see cref="Board(int)"/>,
/// <see cref="PlayerOff"/>, and <see cref="OpponentOff"/>, so an instance is
/// never invalid once it exists — the same posture as
/// <see cref="PointRange"/>. <c>default(PatternSlot)</c> is
/// <c>Board(0)</c> (the opponent's bar), mirroring
/// <c>default(PointRange)</c>'s index 0. Declared a
/// <see langword="readonly record struct"/> for the same reason as
/// <see cref="PointRange"/>: a small immutable value with structural equality
/// for free — which is also what lets <see cref="BoardPattern"/> use it
/// directly as its duplicate-constraint key.
/// </para>
/// </summary>
public readonly record struct PatternSlot
{
    /// <summary>Highest valid board-array index (the on-roll player's bar).</summary>
    public const int MaxBoardIndex = 25;

    /// <summary>
    /// The fifteen-checkers-per-side ceiling — the largest magnitude any
    /// slot's value (and therefore any <see cref="PointRange"/> bound) can
    /// take.
    /// </summary>
    public const int MaxCheckers = 15;

    /// <summary>Bracket-grammar token name for the on-roll player's off slot.</summary>
    internal const string PlayerOffName = "off";

    /// <summary>Bracket-grammar token name for the opponent's off slot.</summary>
    internal const string OpponentOffName = "opp-off";

    private readonly PatternSlotKind _kind;
    private readonly int _index;

    private PatternSlot(PatternSlotKind kind, int index)
    {
        _kind = kind;
        _index = index;
    }

    /// <summary>Which kind of location this slot addresses.</summary>
    public PatternSlotKind Kind => _kind;

    /// <summary>
    /// The board-array index for a <see cref="PatternSlotKind.Board"/> slot;
    /// <see langword="null"/> for the borne-off slots, which have no index —
    /// their values are derived from the whole board.
    /// </summary>
    public int? BoardIndex => _kind == PatternSlotKind.Board ? _index : null;

    /// <summary>
    /// The on-roll player's borne-off count: a derived slot whose value is
    /// <see cref="MaxCheckers"/> minus the player's on-board checkers (bars
    /// included), always in <c>[0, 15]</c>. Named <c>off</c> in the bracket
    /// grammar.
    /// </summary>
    public static PatternSlot PlayerOff { get; } = new(PatternSlotKind.PlayerOff, 0);

    /// <summary>
    /// The opponent's borne-off count: a derived slot whose value is the
    /// negation of <see cref="MaxCheckers"/> minus the opponent's on-board
    /// checkers (bars included), always in <c>[-15, 0]</c> — negative per the
    /// grammar-wide sign rule. Named <c>opp-off</c> in the bracket grammar.
    /// </summary>
    public static PatternSlot OpponentOff { get; } = new(PatternSlotKind.OpponentOff, 0);

    /// <summary>
    /// Creates the slot for board-array index <paramref name="index"/>
    /// (0 = opponent's bar, 1–24 = points, 25 = on-roll player's bar).
    /// </summary>
    /// <param name="index">Board-array index, 0–25.</param>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="index"/> is outside <c>[0, MaxBoardIndex]</c>.
    /// </exception>
    public static PatternSlot Board(int index)
    {
        if (index is < 0 or > MaxBoardIndex)
            throw new ArgumentOutOfRangeException(
                nameof(index), index, $"Board index must be in [0, {MaxBoardIndex}].");

        return new PatternSlot(PatternSlotKind.Board, index);
    }

    /// <summary>
    /// Inclusive lower limit of the signed counts this slot can exhibit:
    /// <c>-15</c> for board slots and the opponent's off slot, <c>0</c> for
    /// the player's off slot. <see cref="PointRange"/> validates its bounds
    /// against this, so a wrong-signed bound is a construction error.
    /// </summary>
    internal int MinValue => _kind == PatternSlotKind.PlayerOff ? 0 : -MaxCheckers;

    /// <summary>
    /// Inclusive upper limit of the signed counts this slot can exhibit:
    /// <c>15</c> for board slots and the player's off slot, <c>0</c> for the
    /// opponent's off slot.
    /// </summary>
    internal int MaxValue => _kind == PatternSlotKind.OpponentOff ? 0 : MaxCheckers;

    /// <summary>
    /// Reads (for a board slot) or derives (for an off slot) this slot's
    /// signed value on <paramref name="board"/>. Board slots index the array
    /// directly; off slots sum the side's on-board checkers — bars included,
    /// never indexing beyond the list — and subtract from
    /// <see cref="MaxCheckers"/>, negated for the opponent per the sign rule.
    /// </summary>
    internal int ValueOn(IReadOnlyList<int> board) => _kind switch
    {
        PatternSlotKind.Board => board[_index],
        PatternSlotKind.PlayerOff => MaxCheckers - PlayerCheckersOn(board),
        PatternSlotKind.OpponentOff => -(MaxCheckers - OpponentCheckersOn(board)),
        _ => throw new UnreachableException($"Undefined {nameof(PatternSlotKind)} '{_kind}'."),
    };

    /// <summary>
    /// Parses a named-slot token head (<c>off</c> / <c>opp-off</c>,
    /// case-insensitive). Returns <see langword="false"/> for anything else;
    /// numeric heads are the caller's business.
    /// </summary>
    internal static bool TryParseName(string name, out PatternSlot slot)
    {
        if (name.Equals(PlayerOffName, StringComparison.OrdinalIgnoreCase))
        {
            slot = PlayerOff;
            return true;
        }

        if (name.Equals(OpponentOffName, StringComparison.OrdinalIgnoreCase))
        {
            slot = OpponentOff;
            return true;
        }

        slot = default;
        return false;
    }

    /// <summary>
    /// Renders this slot as its bracket-grammar token head: the bare index for
    /// a board slot, <c>off</c> / <c>opp-off</c> for the named slots. This is
    /// the canonical (lower-case) spelling <see cref="BoardPattern"/> emits;
    /// parsing accepts the names case-insensitively.
    /// </summary>
    public override string ToString() => _kind switch
    {
        PatternSlotKind.Board => _index.ToString(CultureInfo.InvariantCulture),
        PatternSlotKind.PlayerOff => PlayerOffName,
        PatternSlotKind.OpponentOff => OpponentOffName,
        _ => throw new UnreachableException($"Undefined {nameof(PatternSlotKind)} '{_kind}'."),
    };

    private static int PlayerCheckersOn(IReadOnlyList<int> board)
    {
        int sum = 0;
        for (int i = 0; i < board.Count; i++)
            if (board[i] > 0)
                sum += board[i];
        return sum;
    }

    private static int OpponentCheckersOn(IReadOnlyList<int> board)
    {
        int sum = 0;
        for (int i = 0; i < board.Count; i++)
            if (board[i] < 0)
                sum -= board[i];
        return sum;
    }
}
