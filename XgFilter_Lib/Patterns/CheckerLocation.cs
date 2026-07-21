using System.Diagnostics;
using System.Globalization;

namespace XgFilter_Lib.Patterns;

/// <summary>
/// The kind of place a <see cref="CheckerLocation"/> addresses: one of the 26
/// board-array indices, or one side's borne-off checker count.
/// </summary>
public enum CheckerLocationKind
{
    /// <summary>A board-array index 0–25 (bars included).</summary>
    Board,

    /// <summary>
    /// The on-roll player's borne-off checker count, a derived non-negative
    /// value (see <see cref="CheckerLocation.PlayerOff"/>).
    /// </summary>
    PlayerOff,

    /// <summary>
    /// The opponent's borne-off checker count, a derived non-positive value
    /// (see <see cref="CheckerLocation.OpponentOff"/>).
    /// </summary>
    OpponentOff,
}

/// <summary>
/// The location a <see cref="CheckerRange"/> constrains — either one of the 26
/// on-roll-relative board-array indices (<see cref="CheckerLocationKind.Board"/>)
/// or one side's borne-off checker count, a value <em>derived</em> from the
/// board rather than stored in it. This type is the single source of truth for
/// location vocabulary: which locations exist, the token names the bracket
/// grammar uses for the named ones (<c>off</c>, <c>opp-off</c>), the interval of
/// signed counts each location can exhibit, and how each location's value is
/// read (or derived) from a board.
///
/// <para>
/// Signed convention — one rule across the whole grammar: positive counts are
/// the on-roll player's, negative the opponent's. The borne-off locations follow
/// it: <see cref="PlayerOff"/> takes values in <c>[0, 15]</c> and
/// <see cref="OpponentOff"/> in <c>[-15, 0]</c> (e.g. a value of <c>-2</c>
/// means the opponent has two checkers off). Off counts are derived as
/// fifteen minus the side's on-board sum, bars included — the board array
/// carries no off entry of its own.
/// </para>
///
/// <para>
/// Constructed only through <see cref="Board(int)"/>,
/// <see cref="PlayerOff"/>, and <see cref="OpponentOff"/>, so an instance is
/// never invalid once it exists — the same posture as
/// <see cref="CheckerRange"/>. <c>default(CheckerLocation)</c> is
/// <c>Board(0)</c> (the opponent's bar), mirroring
/// <c>default(CheckerRange)</c>'s index 0. Declared a
/// <see langword="readonly record struct"/> for the same reason as
/// <see cref="CheckerRange"/>: a small immutable value with structural equality
/// for free — which is also what lets <see cref="BoardPattern"/> use it
/// directly as its duplicate-constraint key.
/// </para>
/// </summary>
public readonly record struct CheckerLocation
{
    /// <summary>Highest valid board-array index (the on-roll player's bar).</summary>
    public const int MaxBoardIndex = 25;

    /// <summary>
    /// The fifteen-checkers-per-side ceiling — the largest magnitude any
    /// location's value (and therefore any <see cref="CheckerRange"/> bound) can
    /// take.
    /// </summary>
    public const int MaxCheckers = 15;

    /// <summary>Bracket-grammar token name for the on-roll player's off count.</summary>
    internal const string PlayerOffName = "off";

    /// <summary>Bracket-grammar token name for the opponent's off count.</summary>
    internal const string OpponentOffName = "opp-off";

    private readonly CheckerLocationKind _kind;
    private readonly int _index;

    private CheckerLocation(CheckerLocationKind kind, int index)
    {
        _kind = kind;
        _index = index;
    }

    /// <summary>Which kind of place this location addresses.</summary>
    public CheckerLocationKind Kind => _kind;

    /// <summary>
    /// The board-array index for a <see cref="CheckerLocationKind.Board"/>
    /// location; <see langword="null"/> for the borne-off locations, which have
    /// no index — their values are derived from the whole board.
    /// </summary>
    public int? BoardIndex => _kind == CheckerLocationKind.Board ? _index : null;

    /// <summary>
    /// The on-roll player's borne-off count: a derived location whose value is
    /// <see cref="MaxCheckers"/> minus the player's on-board checkers (bars
    /// included), always in <c>[0, 15]</c>. Named <c>off</c> in the bracket
    /// grammar.
    /// </summary>
    public static CheckerLocation PlayerOff { get; } = new(CheckerLocationKind.PlayerOff, 0);

    /// <summary>
    /// The opponent's borne-off count: a derived location whose value is the
    /// negation of <see cref="MaxCheckers"/> minus the opponent's on-board
    /// checkers (bars included), always in <c>[-15, 0]</c> — negative per the
    /// grammar-wide sign rule. Named <c>opp-off</c> in the bracket grammar.
    /// </summary>
    public static CheckerLocation OpponentOff { get; } = new(CheckerLocationKind.OpponentOff, 0);

    /// <summary>
    /// Creates the location for board-array index <paramref name="index"/>
    /// (0 = opponent's bar, 1–24 = points, 25 = on-roll player's bar).
    /// </summary>
    /// <param name="index">Board-array index, 0–25.</param>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="index"/> is outside <c>[0, MaxBoardIndex]</c>.
    /// </exception>
    public static CheckerLocation Board(int index)
    {
        if (index is < 0 or > MaxBoardIndex)
            throw new ArgumentOutOfRangeException(
                nameof(index), index, $"Board index must be in [0, {MaxBoardIndex}].");

        return new CheckerLocation(CheckerLocationKind.Board, index);
    }

    /// <summary>
    /// Inclusive lower limit of the signed counts this location can exhibit:
    /// <c>-15</c> for board locations and the opponent's off count, <c>0</c> for
    /// the player's off count. <see cref="CheckerRange"/> validates its bounds
    /// against this, so a wrong-signed bound is a construction error.
    /// </summary>
    internal int MinValue => _kind == CheckerLocationKind.PlayerOff ? 0 : -MaxCheckers;

    /// <summary>
    /// Inclusive upper limit of the signed counts this location can exhibit:
    /// <c>15</c> for board locations and the player's off count, <c>0</c> for
    /// the opponent's off count.
    /// </summary>
    internal int MaxValue => _kind == CheckerLocationKind.OpponentOff ? 0 : MaxCheckers;

    /// <summary>
    /// Reads (for a board location) or derives (for an off count) this
    /// location's signed value on <paramref name="board"/>. Board locations
    /// index the array directly; off counts sum the side's on-board checkers —
    /// bars included, never indexing beyond the list — and subtract from
    /// <see cref="MaxCheckers"/>, negated for the opponent per the sign rule.
    /// </summary>
    internal int ValueOn(IReadOnlyList<int> board) => _kind switch
    {
        CheckerLocationKind.Board => board[_index],
        CheckerLocationKind.PlayerOff => MaxCheckers - PlayerCheckersOn(board),
        CheckerLocationKind.OpponentOff => -(MaxCheckers - OpponentCheckersOn(board)),
        _ => throw new UnreachableException($"Undefined {nameof(CheckerLocationKind)} '{_kind}'."),
    };

    /// <summary>
    /// Parses a named-location token head (<c>off</c> / <c>opp-off</c>,
    /// case-insensitive). Returns <see langword="false"/> for anything else;
    /// numeric heads are the caller's business.
    /// </summary>
    internal static bool TryParseName(string name, out CheckerLocation location)
    {
        if (name.Equals(PlayerOffName, StringComparison.OrdinalIgnoreCase))
        {
            location = PlayerOff;
            return true;
        }

        if (name.Equals(OpponentOffName, StringComparison.OrdinalIgnoreCase))
        {
            location = OpponentOff;
            return true;
        }

        location = default;
        return false;
    }

    /// <summary>
    /// Renders this location as its bracket-grammar token head: the bare index
    /// for a board location, <c>off</c> / <c>opp-off</c> for the named ones.
    /// This is the canonical (lower-case) spelling <see cref="BoardPattern"/>
    /// emits; parsing accepts the names case-insensitively.
    /// </summary>
    public override string ToString() => _kind switch
    {
        CheckerLocationKind.Board => _index.ToString(CultureInfo.InvariantCulture),
        CheckerLocationKind.PlayerOff => PlayerOffName,
        CheckerLocationKind.OpponentOff => OpponentOffName,
        _ => throw new UnreachableException($"Undefined {nameof(CheckerLocationKind)} '{_kind}'."),
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
