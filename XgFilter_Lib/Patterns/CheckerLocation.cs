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
/// location vocabulary: which locations exist (the board-index domain
/// <see cref="CheckerSpan"/> is built from included), the token names the
/// bracket grammar uses for the named ones (<c>off</c>, <c>opp-off</c>), the
/// interval of signed counts each location can exhibit, and how each
/// location's value is read (or derived) from a board.
///
/// <para>
/// Signed convention — one rule across the whole grammar: positive counts are
/// the on-roll player's, negative the opponent's. A location's value interval
/// is the hull of the intervals of the sides that can sit there: a point
/// (indices 1–24) holds either side, <c>[-15, 15]</c>; the opponent's bar
/// (index 0) and <see cref="OpponentOff"/> hold only the opponent,
/// <c>[-15, 0]</c>; the on-roll player's bar (index 25) and
/// <see cref="PlayerOff"/> hold only the on-roll player, <c>[0, 15]</c>. So a
/// value of <c>-2</c> at <see cref="OpponentOff"/> means the opponent has two
/// checkers off, and a positive bound on the opponent's bar is a construction
/// error rather than a constraint that silently never matches. Off counts are
/// derived as fifteen minus the side's on-board sum, bars included — the
/// board array carries no off entry of its own.
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

    /// <summary>Board-array index of the opponent's bar.</summary>
    private const int OpponentBarIndex = 0;

    /// <summary>Board-array index of the on-roll player's bar.</summary>
    private const int PlayerBarIndex = MaxBoardIndex;

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
        ThrowIfNotBoardIndex(index, nameof(index));
        return new CheckerLocation(CheckerLocationKind.Board, index);
    }

    /// <summary>
    /// The one check of the board-index domain, shared by <see cref="Board"/>
    /// and <see cref="CheckerSpan"/>.
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="index"/> is outside <c>[0, MaxBoardIndex]</c>.
    /// </exception>
    internal static void ThrowIfNotBoardIndex(int index, string paramName)
    {
        if (index is < 0 or > MaxBoardIndex)
            throw new ArgumentOutOfRangeException(
                paramName, index, $"Board index must be in [0, {MaxBoardIndex}].");
    }

    /// <summary>
    /// The sides whose checkers can sit on this location: the opponent alone
    /// on its bar (index 0) and its off count, the on-roll player alone on
    /// its bar (index <see cref="MaxBoardIndex"/>) and its off count, either
    /// side on a point.
    /// </summary>
    internal CheckerSides Sides => _kind switch
    {
        CheckerLocationKind.Board when _index == OpponentBarIndex => CheckerSides.Opponent,
        CheckerLocationKind.Board when _index == PlayerBarIndex => CheckerSides.Player,
        CheckerLocationKind.Board => CheckerSides.Both,
        CheckerLocationKind.PlayerOff => CheckerSides.Player,
        CheckerLocationKind.OpponentOff => CheckerSides.Opponent,
        _ => throw new UnreachableException($"Undefined {nameof(CheckerLocationKind)} '{_kind}'."),
    };

    /// <summary>
    /// Inclusive lower limit of the signed counts this location can exhibit:
    /// the lower limit of its <see cref="Sides"/> — <c>-15</c> where the
    /// opponent can sit, <c>0</c> where only the on-roll player can.
    /// <see cref="CheckerRange"/> validates its bounds against this, so a
    /// wrong-signed bound is a construction error.
    /// </summary>
    internal int MinValue => Sides.MinValue();

    /// <summary>
    /// Inclusive upper limit of the signed counts this location can exhibit:
    /// the upper limit of its <see cref="Sides"/> — <c>15</c> where the
    /// on-roll player can sit, <c>0</c> where only the opponent can.
    /// </summary>
    internal int MaxValue => Sides.MaxValue();

    /// <summary>
    /// Reads (for a board location) or derives (for an off count) this
    /// location's signed value on <paramref name="board"/>. Board locations
    /// index the array directly; an off count counts its side's on-board
    /// checkers — bars included, never indexing beyond the list — subtracts
    /// that from <see cref="MaxCheckers"/>, and signs the result for the side.
    /// </summary>
    internal int ValueOn(IReadOnlyList<int> board) => _kind switch
    {
        CheckerLocationKind.Board => board[_index],
        CheckerLocationKind.PlayerOff or CheckerLocationKind.OpponentOff =>
            Sides.Signed(MaxCheckers - Sides.CheckersOn(board, 0, board.Count)),
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
}
