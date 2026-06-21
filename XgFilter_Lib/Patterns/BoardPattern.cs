using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Text;
using System.Text.Json.Serialization;

namespace XgFilter_Lib.Patterns;

/// <summary>
/// A sparse, per-point constraint set over the on-roll-relative board array —
/// the general checker-range predicate that a position must satisfy. It is an
/// immutable, validated bag of <see cref="PointRange"/> constraints; an index
/// not named by any range is unconstrained, and an empty pattern matches every
/// board (vacuous truth).
///
/// <para>
/// Board convention (shared with <c>IDecisionFilterData.Board</c>): a
/// 26-element array where <c>[0]</c> is the opponent's bar, <c>[1..24]</c> the
/// points, and <c>[25]</c> the on-roll player's bar; positive values are the
/// on-roll player's checkers and negative the opponent's.
/// </para>
///
/// <para>
/// Text form — the bracket list — is a whitespace-separated sequence of
/// <c>[index,min,max]</c> tokens, each field comma-separated and an empty field
/// meaning "unbounded": <c>"[6,,0] [5,2,] [0,,-1]"</c>.
/// <see cref="Parse"/>/<see cref="TryParse"/> read it and
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
    private readonly PointRange[] _ranges;

    /// <summary>The shared empty pattern, which matches every board.</summary>
    public static BoardPattern Empty { get; } = new(Array.Empty<PointRange>());

    /// <summary>
    /// Creates a validated pattern from <paramref name="ranges"/>. Each
    /// <see cref="PointRange"/> is already self-valid by construction; this
    /// constructor enforces the only cross-element invariant — no two ranges may
    /// constrain the same <see cref="PointRange.Index"/>.
    /// </summary>
    /// <param name="ranges">The per-point constraints; order is not significant.</param>
    /// <exception cref="ArgumentNullException"><paramref name="ranges"/> is null.</exception>
    /// <exception cref="ArgumentException">
    /// Two ranges share the same <see cref="PointRange.Index"/>.
    /// </exception>
    public BoardPattern(IEnumerable<PointRange> ranges)
    {
        ArgumentNullException.ThrowIfNull(ranges);

        _ranges = ranges.ToArray();

        var seen = new HashSet<int>(_ranges.Length);
        foreach (var range in _ranges)
            if (!seen.Add(range.Index))
                throw new ArgumentException(
                    $"Duplicate constraint on board index {range.Index}.", nameof(ranges));
    }

    /// <summary>The constraints making up this pattern, in construction order.</summary>
    public IReadOnlyList<PointRange> Ranges => _ranges;

    /// <summary>
    /// True when the pattern carries no constraints, in which case
    /// <see cref="Matches"/> is vacuously true for every board. Lets a consumer
    /// (e.g. <c>FilterConfig.Build</c>) treat an empty pattern as "no filter."
    /// </summary>
    public bool IsEmpty => _ranges.Length == 0;

    /// <summary>
    /// Tests whether <paramref name="board"/> satisfies every constraint.
    /// Unconstrained indices are ignored; an empty pattern matches all. The
    /// board must be long enough to index every constrained point — it always
    /// is for the canonical 26-element on-roll-relative array.
    /// </summary>
    /// <param name="board">The on-roll-relative board array.</param>
    /// <exception cref="ArgumentNullException"><paramref name="board"/> is null.</exception>
    public bool Matches(IReadOnlyList<int> board)
    {
        ArgumentNullException.ThrowIfNull(board);

        foreach (var range in _ranges)
            if (!range.Contains(board[range.Index]))
                return false;
        return true;
    }

    /// <summary>
    /// Parses a bracket-list pattern such as
    /// <c>"[6,,0] [5,2,] [4,2,] [0,,-1]"</c>. Tokens are whitespace-separated;
    /// within a token the three comma-separated fields are
    /// <c>index,min,max</c>, and an empty field means unbounded. Whitespace-only
    /// (or empty) input yields the empty pattern.
    /// </summary>
    /// <exception cref="ArgumentNullException"><paramref name="text"/> is null.</exception>
    /// <exception cref="FormatException">
    /// A token is malformed (not bracketed, wrong field count, or a non-integer
    /// field).
    /// </exception>
    /// <exception cref="ArgumentOutOfRangeException">
    /// A token's index or bound magnitude is out of range — see
    /// <see cref="PointRange(int, int?, int?)"/>.
    /// </exception>
    /// <exception cref="ArgumentException">
    /// A token has <c>min &gt; max</c>, or two tokens share an index.
    /// </exception>
    public static BoardPattern Parse(string text)
    {
        ArgumentNullException.ThrowIfNull(text);

        var ranges = new List<PointRange>();
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
    /// rejected — malformed token, out-of-range index or bound, <c>min &gt;
    /// max</c>, or duplicate index — and reports failure via the return value.
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
                // bound magnitude), min > max, and duplicate index; FormatException
                // covers malformed tokens. Anything else is unexpected and propagates.
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
    /// Parses one <c>[index,min,max]</c> token. Throws
    /// <see cref="FormatException"/> on a structurally malformed token; bound
    /// and index validation is delegated to <see cref="PointRange"/>.
    /// </summary>
    private static PointRange ParseToken(string token)
    {
        if (token.Length < 2 || token[0] != '[' || token[^1] != ']')
            throw new FormatException(
                $"Malformed pattern token '{token}'. Expected '[index,min,max]'.");

        var fields = token[1..^1].Split(',');
        if (fields.Length != 3)
            throw new FormatException(
                $"Malformed pattern token '{token}'. Expected three comma-separated fields.");

        return new PointRange(
            ParseField(fields[0], token) ?? throw new FormatException(
                $"Malformed pattern token '{token}'. Index is required."),
            ParseField(fields[1], token),
            ParseField(fields[2], token));
    }

    /// <summary>
    /// Parses one bracket-token field: an empty/whitespace field is
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
