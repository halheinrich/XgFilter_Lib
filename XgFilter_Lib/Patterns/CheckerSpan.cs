using System.Globalization;

namespace XgFilter_Lib.Patterns;

/// <summary>
/// A contiguous run of two or more board-array indices, <see cref="First"/>
/// to <see cref="Last"/> inclusive — the place a <see cref="CheckerSpanRange"/>
/// constrains. Indices follow the board convention of
/// <see cref="CheckerLocation"/>, which owns the index domain: 0 is the
/// opponent's bar, 1–24 the points, 25 the on-roll player's bar, and a span may
/// include either bar. The borne-off counts are not board indices and never
/// belong to a span.
///
/// <para>
/// <c>First &lt; Last</c> always: a single index is a
/// <see cref="CheckerLocation"/>, so each constraint has one spelling. The
/// bracket grammar writes a span as <c>a-b</c> (<see cref="ToString"/>).
/// </para>
///
/// <para>
/// Declared a <see langword="readonly record struct"/>, like
/// <see cref="CheckerLocation"/>, for immutability and structural equality —
/// which is what <see cref="BoardPattern"/>'s duplicate check keys on, so an
/// identical span is refused while an overlapping one is not. The last index
/// is stored as its distance past <c>First + 1</c>, which makes
/// <c>default(CheckerSpan)</c> the valid span <c>0-1</c> rather than an
/// invalid <c>0-0</c>: an instance is never invalid once it exists.
/// </para>
/// </summary>
public readonly record struct CheckerSpan
{
    /// <summary>The separator between the two indices in the bracket grammar.</summary>
    internal const char Separator = '-';

    private readonly int _first;
    private readonly int _extent;

    /// <summary>
    /// Creates the span of board-array indices <paramref name="first"/> to
    /// <paramref name="last"/>, inclusive.
    /// </summary>
    /// <param name="first">The lower board-array index, 0–24.</param>
    /// <param name="last">The higher board-array index, 1–25.</param>
    /// <exception cref="ArgumentOutOfRangeException">
    /// Either index is outside <c>[0, CheckerLocation.MaxBoardIndex]</c>.
    /// </exception>
    /// <exception cref="ArgumentException">
    /// <paramref name="first"/> is not less than <paramref name="last"/>; a
    /// single index is written as a <see cref="CheckerLocation"/>.
    /// </exception>
    public CheckerSpan(int first, int last)
    {
        CheckerLocation.ThrowIfNotBoardIndex(first, nameof(first));
        CheckerLocation.ThrowIfNotBoardIndex(last, nameof(last));

        if (first >= last)
            throw new ArgumentException(
                $"Span start ({first}) must be less than its end ({last}); " +
                "a single index is a location, not a span.", nameof(first));

        _first = first;
        _extent = last - first - 1;
    }

    /// <summary>The lowest board-array index in the span.</summary>
    public int First => _first;

    /// <summary>The highest board-array index in the span.</summary>
    public int Last => _first + _extent + 1;

    /// <summary>
    /// Counts <paramref name="side"/>'s checkers across the span as a
    /// non-negative number; the other side's checkers are ignored. Indexes
    /// the board directly, like a board <see cref="CheckerLocation"/>.
    /// </summary>
    internal int CheckersOn(CheckerSides side, IReadOnlyList<int> board) =>
        side.CheckersOn(board, First, Last + 1);

    /// <summary>
    /// Splits a token head of the shape <c>a-b</c>, where both parts are
    /// unsigned decimal integers. Returns <see langword="false"/> for any other
    /// shape; the indices are not validated here — <see cref="CheckerSpan(int, int)"/>
    /// does that, so an out-of-range or reversed span is a range or argument
    /// error rather than a format error.
    /// </summary>
    internal static bool TrySplit(string head, out int first, out int last)
    {
        first = 0;
        last = 0;

        int separator = head.IndexOf(Separator, StringComparison.Ordinal);
        return separator > 0
            && int.TryParse(head.AsSpan(0, separator), NumberStyles.None, CultureInfo.InvariantCulture, out first)
            && int.TryParse(head.AsSpan(separator + 1), NumberStyles.None, CultureInfo.InvariantCulture, out last);
    }

    /// <summary>
    /// Renders this span as its bracket-grammar token head, <c>a-b</c>.
    /// </summary>
    public override string ToString() =>
        string.Create(CultureInfo.InvariantCulture, $"{First}{Separator}{Last}");
}
