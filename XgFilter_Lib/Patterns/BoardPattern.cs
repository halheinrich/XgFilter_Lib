using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Text;
using System.Text.Json.Serialization;

namespace XgFilter_Lib.Patterns;

/// <summary>
/// A sparse constraint set over the on-roll-relative board — the general
/// checker-count predicate that a position must satisfy. It is an immutable,
/// validated set of <see cref="IPatternConstraint"/> conditions, every one of
/// which must hold: a <see cref="CheckerRange"/> bounds the signed count at one
/// <see cref="CheckerLocation"/>, and a <see cref="CheckerSpanRange"/> bounds
/// one side's total across a <see cref="CheckerSpan"/>. A place named by no
/// constraint is unconstrained, and an empty pattern matches every board
/// (vacuous truth).
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
/// <c>[head,min,max]</c> tokens, each field comma-separated and an empty field
/// meaning "unbounded". The head is a board index (<c>[6,,0]</c>), a named
/// borne-off location — <c>[off,min,max]</c> for the on-roll player (bounds in
/// <c>[0, 15]</c>) and <c>[opp-off,min,max]</c> for the opponent (bounds in
/// <c>[-15, 0]</c>, negative per the grammar-wide sign rule — so
/// <c>[opp-off,,-2]</c> reads "opponent has two or more off" exactly like
/// <c>[5,,-2]</c> reads "two or more on the 5-point") — or a span of board
/// indices <c>a-b</c> with <c>a &lt; b</c> (<c>[7-12,3,]</c>: the on-roll
/// player has three or more across 7 to 12; see <see cref="CheckerSpanRange"/>).
/// Names parse case-insensitively and render canonically lower-case.
/// <see cref="Parse"/>/<see cref="TryParse"/> read the form and
/// <see cref="ToBracketList"/>/<see cref="ToString"/> write it; the two
/// round-trip.
/// </para>
///
/// <para>
/// Equality: value-based, over the constraint set. Two patterns are equal when
/// they carry the same constraints in any order — order is not significant to
/// the constructor either, and the no-duplicate-place invariant makes the
/// constraints a genuine set rather than a bag.
/// <see cref="GetHashCode"/> aggregates them order-independently to
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
    private readonly IPatternConstraint[] _constraints;

    /// <summary>The shared empty pattern, which matches every board.</summary>
    public static BoardPattern Empty { get; } = new(Array.Empty<IPatternConstraint>());

    /// <summary>
    /// Creates a validated pattern from <paramref name="constraints"/>. Each
    /// <see cref="CheckerRange"/> and <see cref="CheckerSpanRange"/> is already
    /// self-valid by construction; this constructor enforces the only
    /// cross-element invariant — no two constraints on the same place (the same
    /// <see cref="CheckerLocation"/>, or the same <see cref="CheckerSpan"/>).
    /// Overlap is not duplication: <c>[7,1,]</c>, <c>[7-12,3,]</c> and
    /// <c>[5-8,,-2]</c> may stand together, each one more condition that must
    /// hold.
    /// </summary>
    /// <param name="constraints">The constraints; order is not significant.</param>
    /// <exception cref="ArgumentNullException"><paramref name="constraints"/> is null.</exception>
    /// <exception cref="ArgumentException">
    /// An element is null, or two constraints share the same place.
    /// </exception>
    public BoardPattern(IEnumerable<IPatternConstraint> constraints)
    {
        ArgumentNullException.ThrowIfNull(constraints);

        _constraints = constraints.ToArray();

        var seen = new HashSet<object>(_constraints.Length);
        foreach (var constraint in _constraints)
        {
            if (constraint is null)
                throw new ArgumentException("A pattern constraint must not be null.", nameof(constraints));

            if (!seen.Add(constraint.Place))
                throw new ArgumentException(
                    $"Duplicate constraint on '{constraint.Place}'.", nameof(constraints));
        }
    }

    /// <summary>
    /// The constraints making up this pattern, in construction order — each a
    /// <see cref="CheckerRange"/> or a <see cref="CheckerSpanRange"/>.
    /// </summary>
    public IReadOnlyList<IPatternConstraint> Constraints => _constraints;

    /// <summary>
    /// True when the pattern carries no constraints, in which case
    /// <see cref="Matches"/> is vacuously true for every board. Lets a consumer
    /// (e.g. <c>FilterConfig.Build</c>) treat an empty pattern as "no filter."
    /// </summary>
    public bool IsEmpty => _constraints.Length == 0;

    /// <summary>
    /// Tests whether <paramref name="board"/> satisfies every constraint.
    /// Unconstrained places are ignored; an empty pattern matches all. Board
    /// locations and spans index the array directly, so it must be long enough
    /// to index every constrained index — it always is for the canonical
    /// 26-element on-roll-relative array; borne-off locations only sum the
    /// elements the list actually has, never indexing beyond it.
    /// </summary>
    /// <param name="board">The on-roll-relative board array.</param>
    /// <exception cref="ArgumentNullException"><paramref name="board"/> is null.</exception>
    public bool Matches(IReadOnlyList<int> board)
    {
        ArgumentNullException.ThrowIfNull(board);

        foreach (var constraint in _constraints)
            if (!constraint.IsSatisfiedBy(board))
                return false;
        return true;
    }

    /// <summary>
    /// Parses a bracket-list pattern such as
    /// <c>"[6,,0] [5,2,] [off,1,] [opp-off,,-2] [7-12,3,]"</c>. Tokens are
    /// whitespace-separated; within a token the three comma-separated fields
    /// are <c>head,min,max</c>, where the head is a board index, a named
    /// borne-off location (<c>off</c> / <c>opp-off</c>, case-insensitive), or a
    /// span of board indices <c>a-b</c>, and an empty bound field means
    /// unbounded. Whitespace-only (or empty) input yields the empty pattern.
    /// </summary>
    /// <exception cref="ArgumentNullException"><paramref name="text"/> is null.</exception>
    /// <exception cref="FormatException">
    /// A token is malformed (not bracketed, wrong field count, an unrecognized
    /// head — a borne-off name inside a span among them — or a non-integer
    /// bound field).
    /// </exception>
    /// <exception cref="ArgumentOutOfRangeException">
    /// A token's index or bound is out of range — see
    /// <see cref="CheckerRange(CheckerLocation, int?, int?)"/> and
    /// <see cref="CheckerSpanRange(CheckerSpan, int?, int?)"/>; a wrong-signed
    /// bound on a bar or a borne-off count lands here.
    /// </exception>
    /// <exception cref="ArgumentException">
    /// A token has <c>min &gt; max</c>, a span's start is not below its end, a
    /// span's bounds have opposite signs, or two tokens share a place.
    /// </exception>
    public static BoardPattern Parse(string text)
    {
        ArgumentNullException.ThrowIfNull(text);

        var constraints = new List<IPatternConstraint>();
        foreach (var token in text.Split(
            (char[]?)null, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            constraints.Add(ParseToken(token));
        }

        return new BoardPattern(constraints);
    }

    /// <summary>
    /// Non-throwing counterpart to <see cref="Parse"/>, following the
    /// <c>TryParse</c> convention. Absorbs every way a bracket list can be
    /// rejected — malformed token, unrecognized head, out-of-range index or
    /// bound (including a wrong-signed bound on a bar or a borne-off count),
    /// <c>min &gt; max</c>, a span that is reversed, a single index, or
    /// opposite-signed, or a duplicate place — and reports failure via the
    /// return value.
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
                // bound, including wrong-signed bar and borne-off bounds), min >
                // max, a reversed / single-index / opposite-signed span, and
                // duplicate place; FormatException covers malformed tokens and
                // unrecognized heads. Anything else is unexpected and propagates.
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
        for (int i = 0; i < _constraints.Length; i++)
        {
            if (i > 0) sb.Append(' ');
            sb.Append(_constraints[i].ToString());
        }
        return sb.ToString();
    }

    /// <inheritdoc cref="ToBracketList"/>
    public override string ToString() => ToBracketList();

    /// <summary>
    /// Value equality over the constraint set: true when
    /// <paramref name="other"/> carries the same constraints as this pattern,
    /// regardless of their order. Elements compare by their own
    /// <see langword="record struct"/> value equality, so a numeric and a named
    /// location never conflate, and a location constraint never equals a span
    /// constraint.
    /// <para>
    /// Equality is semantic; the text form is faithful to how a pattern was
    /// built. <see cref="ToBracketList"/> prints in construction order and
    /// nothing normalizes it, so <em>two equal patterns may print different
    /// bracket lists</em> — comparing patterns by their text is stricter than
    /// this method, and reports a difference where there is none. Compare with
    /// <see cref="Equals(BoardPattern)"/>.
    /// </para>
    /// </summary>
    /// <param name="other">The pattern to compare against, or null.</param>
    public bool Equals(BoardPattern? other)
    {
        if (ReferenceEquals(this, other))
            return true;

        if (other is null || _constraints.Length != other._constraints.Length)
            return false;

        // No two constraints may share a place (the constructor's invariant),
        // so the elements are distinct and a set comparison is exact — no
        // multiset tally needed.
        return _constraints.ToHashSet().SetEquals(other._constraints);
    }

    /// <inheritdoc/>
    public override bool Equals(object? obj) => Equals(obj as BoardPattern);

    /// <summary>
    /// A hash consistent with <see cref="Equals(BoardPattern)"/>: the element
    /// hashes are combined with XOR, which is order-independent, so patterns
    /// that differ only in constraint order hash alike — including the two that
    /// print different bracket lists.
    /// </summary>
    public override int GetHashCode()
    {
        int aggregate = 0;
        foreach (var constraint in _constraints)
            aggregate ^= constraint.GetHashCode();

        return HashCode.Combine(_constraints.Length, aggregate);
    }

    /// <summary>
    /// Parses one <c>[head,min,max]</c> token. Throws
    /// <see cref="FormatException"/> on a structurally malformed token or an
    /// unrecognized head; index and bound validation is delegated to the
    /// constraint types. The head is read, and so validated, before the bounds.
    /// </summary>
    private static IPatternConstraint ParseToken(string token)
    {
        if (token.Length < 2 || token[0] != '[' || token[^1] != ']')
            throw new FormatException(
                $"Malformed pattern token '{token}'. Expected '[head,min,max]'.");

        var fields = token[1..^1].Split(',');
        if (fields.Length != 3)
            throw new FormatException(
                $"Malformed pattern token '{token}'. Expected three comma-separated fields.");

        var head = fields[0].Trim();
        if (head.Length == 0)
            throw new FormatException(
                $"Malformed pattern token '{token}'. Location is required.");

        if (CheckerSpan.TrySplit(head, out int first, out int last))
        {
            var span = new CheckerSpan(first, last);
            return new CheckerSpanRange(span, ParseField(fields[1], token), ParseField(fields[2], token));
        }

        var location = ParseLocation(head, token);
        return new CheckerRange(location, ParseField(fields[1], token), ParseField(fields[2], token));
    }

    /// <summary>
    /// Parses a single-location head: a board-array index, or a named
    /// borne-off location (case-insensitive — the vocabulary lives on
    /// <see cref="CheckerLocation"/>). An out-of-range index is a range error from
    /// <see cref="CheckerLocation.Board"/>, not a format error; anything that is
    /// neither an integer, a known name, nor a span is a
    /// <see cref="FormatException"/>.
    /// </summary>
    private static CheckerLocation ParseLocation(string head, string token)
    {
        if (int.TryParse(head, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out int index))
            return CheckerLocation.Board(index);

        if (CheckerLocation.TryParseName(head, out var location))
            return location;

        throw new FormatException(
            $"Malformed pattern token '{token}'. Head '{head}' is neither a board index, " +
            $"a named location ('{CheckerLocation.PlayerOffName}', '{CheckerLocation.OpponentOffName}'), " +
            $"nor a span of board indices ('a{CheckerSpan.Separator}b').");
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
