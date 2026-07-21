using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Text;
using System.Text.Json.Serialization;

namespace XgFilter_Lib.Patterns;

/// <summary>
/// A sparse, per-slot constraint set over the on-roll-relative board — the
/// general checker-range predicate that a position must satisfy. It is an
/// immutable, validated bag of <see cref="SlotRange"/> constraints; a
/// <see cref="Slot"/> not named by any range is unconstrained, and an
/// empty pattern matches every board (vacuous truth).
///
/// <para>
/// Board convention (shared with <c>IDecisionFilterData.Board</c>): a
/// 26-element array where <c>[0]</c> is the opponent's bar, <c>[1..24]</c> the
/// points, and <c>[25]</c> the on-roll player's bar; positive values are the
/// on-roll player's checkers and negative the opponent's. The two borne-off
/// slots are <em>derived</em> from that array (fifteen minus the side's
/// on-board sum, bars included — see <see cref="Slot"/>), so a pattern
/// can constrain off counts with no extra data plumbed in.
/// </para>
///
/// <para>
/// Text form — the bracket list — is a whitespace-separated sequence of
/// <c>[slot,min,max]</c> tokens, each field comma-separated and an empty field
/// meaning "unbounded". The slot head is a board index (<c>[6,,0]</c>) or a
/// named borne-off slot: <c>[off,min,max]</c> for the on-roll player (bounds
/// in <c>[0, 15]</c>) and <c>[opp-off,min,max]</c> for the opponent (bounds in
/// <c>[-15, 0]</c>, negative per the grammar-wide sign rule — so
/// <c>[opp-off,,-2]</c> reads "opponent has two or more off" exactly like
/// <c>[5,,-2]</c> reads "two or more on the 5-point"). Names parse
/// case-insensitively and render canonically lower-case.
/// <see cref="Parse"/>/<see cref="TryParse"/> read the form and
/// <see cref="ToBracketList"/>/<see cref="ToString"/> write it; the two
/// round-trip.
/// </para>
///
/// <para>
/// Equality: this type deliberately does <em>not</em> implement value-equality.
/// Its backing store is an <see cref="IReadOnlyList{T}"/>, and reference
/// equality on the list would make two structurally identical patterns compare
/// unequal — the same footgun <c>FilterConfig</c> declined. Tests should compare
/// structurally (e.g. FluentAssertions <c>BeEquivalentTo</c>) or via
/// <see cref="ToBracketList"/>.
/// </para>
///
/// <para>
/// Serialization: the type carries its own <see cref="BoardPatternJsonConverter"/>
/// via <see cref="JsonConverterAttribute"/>, so it round-trips as its bracket-list
/// string under <em>any</em> <see cref="System.Text.Json.JsonSerializerOptions"/> —
/// the default reflection serializer cannot reconstruct this immutable type (its
/// constructor parameter has no matching settable property), so the converter is
/// the single source of truth for the wire form rather than something each
/// consumer must remember to register.
/// </para>
/// </summary>
[JsonConverter(typeof(BoardPatternJsonConverter))]
public sealed class BoardPattern
{
    private readonly SlotRange[] _ranges;

    /// <summary>The shared empty pattern, which matches every board.</summary>
    public static BoardPattern Empty { get; } = new(Array.Empty<SlotRange>());

    /// <summary>
    /// Creates a validated pattern from <paramref name="ranges"/>. Each
    /// <see cref="SlotRange"/> is already self-valid by construction; this
    /// constructor enforces the only cross-element invariant — no two ranges may
    /// constrain the same <see cref="SlotRange.Slot"/>.
    /// </summary>
    /// <param name="ranges">The per-slot constraints; order is not significant.</param>
    /// <exception cref="ArgumentNullException"><paramref name="ranges"/> is null.</exception>
    /// <exception cref="ArgumentException">
    /// Two ranges share the same <see cref="SlotRange.Slot"/>.
    /// </exception>
    public BoardPattern(IEnumerable<SlotRange> ranges)
    {
        ArgumentNullException.ThrowIfNull(ranges);

        _ranges = ranges.ToArray();

        var seen = new HashSet<Slot>(_ranges.Length);
        foreach (var range in _ranges)
            if (!seen.Add(range.Slot))
                throw new ArgumentException(
                    $"Duplicate constraint on slot '{range.Slot}'.", nameof(ranges));
    }

    /// <summary>The constraints making up this pattern, in construction order.</summary>
    public IReadOnlyList<SlotRange> Ranges => _ranges;

    /// <summary>
    /// True when the pattern carries no constraints, in which case
    /// <see cref="Matches"/> is vacuously true for every board. Lets a consumer
    /// (e.g. <c>FilterConfig.Build</c>) treat an empty pattern as "no filter."
    /// </summary>
    public bool IsEmpty => _ranges.Length == 0;

    /// <summary>
    /// Tests whether <paramref name="board"/> satisfies every constraint.
    /// Unconstrained slots are ignored; an empty pattern matches all. Board
    /// slots index the array directly, so it must be long enough to index
    /// every constrained point — it always is for the canonical 26-element
    /// on-roll-relative array; borne-off slots only sum the elements the list
    /// actually has, never indexing beyond it.
    /// </summary>
    /// <param name="board">The on-roll-relative board array.</param>
    /// <exception cref="ArgumentNullException"><paramref name="board"/> is null.</exception>
    public bool Matches(IReadOnlyList<int> board)
    {
        ArgumentNullException.ThrowIfNull(board);

        foreach (var range in _ranges)
            if (!range.IsSatisfiedBy(board))
                return false;
        return true;
    }

    /// <summary>
    /// Parses a bracket-list pattern such as
    /// <c>"[6,,0] [5,2,] [off,1,] [opp-off,,-2]"</c>. Tokens are
    /// whitespace-separated; within a token the three comma-separated fields
    /// are <c>slot,min,max</c>, where the slot is a board index or a named
    /// borne-off slot (<c>off</c> / <c>opp-off</c>, case-insensitive) and an
    /// empty bound field means unbounded. Whitespace-only (or empty) input
    /// yields the empty pattern.
    /// </summary>
    /// <exception cref="ArgumentNullException"><paramref name="text"/> is null.</exception>
    /// <exception cref="FormatException">
    /// A token is malformed (not bracketed, wrong field count, an unrecognized
    /// slot head, or a non-integer bound field).
    /// </exception>
    /// <exception cref="ArgumentOutOfRangeException">
    /// A token's index or bound is out of range — see
    /// <see cref="SlotRange(Slot, int?, int?)"/>; a wrong-signed
    /// borne-off bound lands here.
    /// </exception>
    /// <exception cref="ArgumentException">
    /// A token has <c>min &gt; max</c>, or two tokens share a slot.
    /// </exception>
    public static BoardPattern Parse(string text)
    {
        ArgumentNullException.ThrowIfNull(text);

        var ranges = new List<SlotRange>();
        foreach (var token in text.Split(
            (char[]?)null, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            ranges.Add(ParseToken(token));
        }

        return new BoardPattern(ranges);
    }

    /// <summary>
    /// Non-throwing counterpart to <see cref="Parse"/>, following the
    /// <c>TryParse</c> convention. Absorbs every way a bracket list can be
    /// rejected — malformed token, unrecognized slot name, out-of-range index
    /// or bound (including a wrong-signed borne-off bound), <c>min &gt;
    /// max</c>, or duplicate slot — and reports failure via the return value.
    /// </summary>
    /// <param name="text">The candidate bracket list, or null.</param>
    /// <param name="pattern">
    /// On success, the parsed pattern; on failure, <see langword="null"/>.
    /// </param>
    /// <returns><see langword="true"/> if <paramref name="text"/> parsed.</returns>
    public static bool TryParse(
        [NotNullWhen(true)] string? text,
        [NotNullWhen(true)] out BoardPattern? pattern)
    {
        if (text is not null)
        {
            try
            {
                pattern = Parse(text);
                return true;
            }
            catch (Exception ex) when (ex is FormatException or ArgumentException)
            {
                // ArgumentException covers ArgumentOutOfRangeException (index /
                // bound, including wrong-signed borne-off bounds), min > max, and
                // duplicate slot; FormatException covers malformed tokens and
                // unrecognized slot names. Anything else is unexpected and propagates.
            }
        }

        pattern = null;
        return false;
    }

    /// <summary>
    /// Renders this pattern as a bracket list that round-trips through
    /// <see cref="Parse"/>. The empty pattern renders as the empty string.
    /// </summary>
    public string ToBracketList()
    {
        var sb = new StringBuilder();
        for (int i = 0; i < _ranges.Length; i++)
        {
            if (i > 0) sb.Append(' ');
            sb.Append(_ranges[i].ToString());
        }
        return sb.ToString();
    }

    /// <inheritdoc cref="ToBracketList"/>
    public override string ToString() => ToBracketList();

    /// <summary>
    /// Parses one <c>[slot,min,max]</c> token. Throws
    /// <see cref="FormatException"/> on a structurally malformed token or an
    /// unrecognized slot head; bound and index validation is delegated to
    /// <see cref="SlotRange"/> / <see cref="Slot"/>.
    /// </summary>
    private static SlotRange ParseToken(string token)
    {
        if (token.Length < 2 || token[0] != '[' || token[^1] != ']')
            throw new FormatException(
                $"Malformed pattern token '{token}'. Expected '[slot,min,max]'.");

        var fields = token[1..^1].Split(',');
        if (fields.Length != 3)
            throw new FormatException(
                $"Malformed pattern token '{token}'. Expected three comma-separated fields.");

        return new SlotRange(
            ParseSlot(fields[0], token),
            ParseField(fields[1], token),
            ParseField(fields[2], token));
    }

    /// <summary>
    /// Parses a token's slot head: a board-array index, or a named borne-off
    /// slot (case-insensitive — the vocabulary lives on
    /// <see cref="Slot"/>). An out-of-range index is a range error from
    /// <see cref="Slot.Board"/>, not a format error; anything that is
    /// neither an integer nor a known name is a <see cref="FormatException"/>.
    /// </summary>
    private static Slot ParseSlot(string field, string token)
    {
        field = field.Trim();
        if (field.Length == 0)
            throw new FormatException(
                $"Malformed pattern token '{token}'. Slot is required.");

        if (int.TryParse(field, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out int index))
            return Slot.Board(index);

        if (Slot.TryParseName(field, out var slot))
            return slot;

        throw new FormatException(
            $"Malformed pattern token '{token}'. Slot '{field}' is neither a board index " +
            $"nor a named slot ('{Slot.PlayerOffName}', '{Slot.OpponentOffName}').");
    }

    /// <summary>
    /// Parses one bracket-token bound field: an empty/whitespace field is
    /// <see langword="null"/> (unbounded); otherwise it must be an integer.
    /// </summary>
    private static int? ParseField(string field, string token)
    {
        field = field.Trim();
        if (field.Length == 0)
            return null;

        if (int.TryParse(field, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out int value))
            return value;

        throw new FormatException(
            $"Malformed pattern token '{token}'. Field '{field}' is not an integer.");
    }
}
