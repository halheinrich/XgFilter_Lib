using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Text;
using System.Text.Json.Serialization;

namespace XgFilter_Lib.Patterns;

/// <summary>
/// A sparse, per-location constraint set over the on-roll-relative board — the
/// general checker-range predicate that a position must satisfy. It is an
/// immutable, validated bag of <see cref="CheckerRange"/> constraints; a
/// <see cref="CheckerLocation"/> not named by any range is unconstrained, and an
/// empty pattern matches every board (vacuous truth).
///
/// <para>
/// Board convention (shared with <c>IDecisionFilterData.Board</c>): a
/// 26-element array where <c>[0]</c> is the opponent's bar, <c>[1..24]</c> the
/// points, and <c>[25]</c> the on-roll player's bar; positive values are the
/// on-roll player's checkers and negative the opponent's. The two borne-off
/// locations are <em>derived</em> from that array (fifteen minus the side's
/// on-board sum, bars included — see <see cref="CheckerLocation"/>), so a
/// pattern can constrain off counts with no extra data plumbed in.
/// </para>
///
/// <para>
/// Text form — the bracket list — is a whitespace-separated sequence of
/// <c>[location,min,max]</c> tokens, each field comma-separated and an empty field
/// meaning "unbounded". The location head is a board index (<c>[6,,0]</c>) or a
/// named borne-off location: <c>[off,min,max]</c> for the on-roll player (bounds
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
/// Equality: value-based, over the constraint set. Two patterns are equal when
/// they carry the same <see cref="CheckerRange"/> constraints in any order —
/// order is not significant to the constructor either, and the
/// no-duplicate-location invariant makes the constraints a genuine set rather
/// than a bag. <see cref="GetHashCode"/> aggregates them order-independently to
/// agree. Two patterns parsed from the same bracket list are therefore always
/// equal; the converse holds only up to token order, since
/// <see cref="ToBracketList"/> preserves construction order and two equal
/// patterns may render permuted lists.
/// No <c>==</c> / <c>!=</c> operators are declared: this is a reference type,
/// and by the prevailing convention its operators keep reference semantics.
/// Use <see cref="Equals(BoardPattern)"/> — or any hash-based or LINQ-based
/// collection, which reach it through <see cref="IEquatable{T}"/> — for value
/// comparison.
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
public sealed class BoardPattern : IEquatable<BoardPattern>
{
    private readonly CheckerRange[] _ranges;

    /// <summary>The shared empty pattern, which matches every board.</summary>
    public static BoardPattern Empty { get; } = new(Array.Empty<CheckerRange>());

    /// <summary>
    /// Creates a validated pattern from <paramref name="ranges"/>. Each
    /// <see cref="CheckerRange"/> is already self-valid by construction; this
    /// constructor enforces the only cross-element invariant — no two ranges may
    /// constrain the same <see cref="CheckerRange.Location"/>.
    /// </summary>
    /// <param name="ranges">The per-location constraints; order is not significant.</param>
    /// <exception cref="ArgumentNullException"><paramref name="ranges"/> is null.</exception>
    /// <exception cref="ArgumentException">
    /// Two ranges share the same <see cref="CheckerRange.Location"/>.
    /// </exception>
    public BoardPattern(IEnumerable<CheckerRange> ranges)
    {
        ArgumentNullException.ThrowIfNull(ranges);

        _ranges = ranges.ToArray();

        var seen = new HashSet<CheckerLocation>(_ranges.Length);
        foreach (var range in _ranges)
            if (!seen.Add(range.Location))
                throw new ArgumentException(
                    $"Duplicate constraint on location '{range.Location}'.", nameof(ranges));
    }

    /// <summary>The constraints making up this pattern, in construction order.</summary>
    public IReadOnlyList<CheckerRange> Ranges => _ranges;

    /// <summary>
    /// True when the pattern carries no constraints, in which case
    /// <see cref="Matches"/> is vacuously true for every board. Lets a consumer
    /// (e.g. <c>FilterConfig.Build</c>) treat an empty pattern as "no filter."
    /// </summary>
    public bool IsEmpty => _ranges.Length == 0;

    /// <summary>
    /// Tests whether <paramref name="board"/> satisfies every constraint.
    /// Unconstrained locations are ignored; an empty pattern matches all. Board
    /// locations index the array directly, so it must be long enough to index
    /// every constrained point — it always is for the canonical 26-element
    /// on-roll-relative array; borne-off locations only sum the elements the
    /// list actually has, never indexing beyond it.
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
    /// are <c>location,min,max</c>, where the location is a board index or a named
    /// borne-off location (<c>off</c> / <c>opp-off</c>, case-insensitive) and an
    /// empty bound field means unbounded. Whitespace-only (or empty) input
    /// yields the empty pattern.
    /// </summary>
    /// <exception cref="ArgumentNullException"><paramref name="text"/> is null.</exception>
    /// <exception cref="FormatException">
    /// A token is malformed (not bracketed, wrong field count, an unrecognized
    /// location head, or a non-integer bound field).
    /// </exception>
    /// <exception cref="ArgumentOutOfRangeException">
    /// A token's index or bound is out of range — see
    /// <see cref="CheckerRange(CheckerLocation, int?, int?)"/>; a wrong-signed
    /// borne-off bound lands here.
    /// </exception>
    /// <exception cref="ArgumentException">
    /// A token has <c>min &gt; max</c>, or two tokens share a location.
    /// </exception>
    public static BoardPattern Parse(string text)
    {
        ArgumentNullException.ThrowIfNull(text);

        var ranges = new List<CheckerRange>();
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
    /// rejected — malformed token, unrecognized location name, out-of-range index
    /// or bound (including a wrong-signed borne-off bound), <c>min &gt;
    /// max</c>, or duplicate location — and reports failure via the return value.
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
                // duplicate location; FormatException covers malformed tokens and
                // unrecognized location names. Anything else is unexpected and propagates.
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
    /// Value equality over the constraint set: true when
    /// <paramref name="other"/> carries the same <see cref="CheckerRange"/>
    /// constraints as this pattern, regardless of their order (see the
    /// type-level remarks). Elements compare by their own
    /// <see langword="record struct"/> value equality, so a numeric and a named
    /// location never conflate.
    /// </summary>
    /// <param name="other">The pattern to compare against, or null.</param>
    public bool Equals(BoardPattern? other)
    {
        if (ReferenceEquals(this, other))
            return true;

        if (other is null || _ranges.Length != other._ranges.Length)
            return false;

        // No two ranges may share a location (the constructor's invariant), so
        // the elements are distinct and a set comparison is exact — no multiset
        // tally needed.
        return _ranges.ToHashSet().SetEquals(other._ranges);
    }

    /// <inheritdoc/>
    public override bool Equals(object? obj) => Equals(obj as BoardPattern);

    /// <summary>
    /// A hash consistent with <see cref="Equals(BoardPattern)"/>: the element
    /// hashes are combined with XOR, which is order-independent, so patterns
    /// that differ only in constraint order hash alike.
    /// </summary>
    public override int GetHashCode()
    {
        int aggregate = 0;
        foreach (var range in _ranges)
            aggregate ^= range.GetHashCode();

        return HashCode.Combine(_ranges.Length, aggregate);
    }

    /// <summary>
    /// Parses one <c>[location,min,max]</c> token. Throws
    /// <see cref="FormatException"/> on a structurally malformed token or an
    /// unrecognized location head; bound and index validation is delegated to
    /// <see cref="CheckerRange"/> / <see cref="CheckerLocation"/>.
    /// </summary>
    private static CheckerRange ParseToken(string token)
    {
        if (token.Length < 2 || token[0] != '[' || token[^1] != ']')
            throw new FormatException(
                $"Malformed pattern token '{token}'. Expected '[location,min,max]'.");

        var fields = token[1..^1].Split(',');
        if (fields.Length != 3)
            throw new FormatException(
                $"Malformed pattern token '{token}'. Expected three comma-separated fields.");

        return new CheckerRange(
            ParseLocation(fields[0], token),
            ParseField(fields[1], token),
            ParseField(fields[2], token));
    }

    /// <summary>
    /// Parses a token's location head: a board-array index, or a named
    /// borne-off location (case-insensitive — the vocabulary lives on
    /// <see cref="CheckerLocation"/>). An out-of-range index is a range error from
    /// <see cref="CheckerLocation.Board"/>, not a format error; anything that is
    /// neither an integer nor a known name is a <see cref="FormatException"/>.
    /// </summary>
    private static CheckerLocation ParseLocation(string field, string token)
    {
        field = field.Trim();
        if (field.Length == 0)
            throw new FormatException(
                $"Malformed pattern token '{token}'. Location is required.");

        if (int.TryParse(field, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out int index))
            return CheckerLocation.Board(index);

        if (CheckerLocation.TryParseName(field, out var location))
            return location;

        throw new FormatException(
            $"Malformed pattern token '{token}'. Location '{field}' is neither a board index " +
            $"nor a named location ('{CheckerLocation.PlayerOffName}', '{CheckerLocation.OpponentOffName}').");
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
