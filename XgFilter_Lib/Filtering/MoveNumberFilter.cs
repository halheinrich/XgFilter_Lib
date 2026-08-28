using BgDataTypes_Lib;

namespace XgFilter_Lib.Filtering;

/// <summary>
/// Passes rows whose <see cref="IDecisionFilterData.MoveNumber"/> falls within
/// [min, max] (inclusive) AND whose game started from the canonical opening
/// position. Either bound may be omitted (null) to leave that end open.
/// <para>
/// Non-standard-start games (custom problem positions, Bg960, etc.) are
/// dropped wholesale via <see cref="IMatchFilter.ShouldSkipGame"/>; for them
/// no canonical move numbering is meaningful. Within standard-start games,
/// once a decision past <c>max</c> has been seen
/// <see cref="ShouldAdvanceGame"/> signals the iterator to skip the rest of
/// the game — move number is monotonically increasing per game, so no
/// later decision can match.
/// </para>
///
/// <para>
/// Bounds are constrained, because the quantity filtered is a 1-based
/// ordinal: <see cref="IDecisionFilterData.MoveNumber"/> is documented as the
/// 1-based move number within the game, so a lower bound below one is a no-op
/// dressed as a filter and an upper bound below one admits nothing at all,
/// while <c>min &gt; max</c> is empty by construction. None of the three is a
/// filter a user could mean, so each is a construction error rather than a
/// range that silently never matches — the same posture
/// <see cref="ErrorRangeFilter"/> takes towards its magnitude bounds.
/// <see cref="IsBoundAtLeastOne"/> and <see cref="AreBoundsOrdered"/> state
/// that rule once for the whole library; this constructor enforces it and
/// <see cref="FilterConfig.GetInvalidFields"/> reports it, so a consumer can
/// ask before it builds and the two answers cannot disagree.
/// </para>
/// </summary>
internal sealed class MoveNumberFilter : IDecisionFilter, IMatchFilter
{
    private readonly int? _min;
    private readonly int? _max;

    /// <summary>
    /// Half of the facet's bound rule, and its single statement: a move-number
    /// bound must be one or greater. An absent bound (null) satisfies it
    /// vacuously — the rule constrains values, never presence — and one
    /// satisfies it outright, being the first move of a game and so a
    /// meaningful end for either bound.
    /// <para>
    /// The floor is the numbering's own: move numbers are 1-based within a
    /// game, so zero and below name no decision. A sub-floor lower bound
    /// merely restates the open end, and a sub-floor upper bound excludes
    /// every row there is; both are the failure mode this rule exists to
    /// catch, and both are reachable from a text-entry consumer.
    /// </para>
    /// </summary>
    /// <param name="bound">The bound to judge, or null for an open end.</param>
    /// <returns><see langword="true"/> if <paramref name="bound"/> is admissible.</returns>
    internal static bool IsBoundAtLeastOne(int? bound) => bound is null || bound.Value >= 1;

    /// <summary>
    /// The other half of the facet's bound rule, and its single statement: when
    /// both bounds are present the lower must not exceed the upper. A one-sided
    /// or absent range satisfies it vacuously, and an equal pair satisfies it
    /// (the bounds being inclusive, that is the single-move filter).
    /// <para>
    /// This is a rule about the <em>pair</em>, so a violation blames neither
    /// bound alone — see <see cref="FilterConfig.GetInvalidFields"/>, which
    /// reports both. It presumes bounds already admissible under
    /// <see cref="IsBoundAtLeastOne"/>; against an inadmissible one its verdict
    /// is a restatement of that fault rather than news, which is why the
    /// constructor checks the bounds individually first.
    /// </para>
    /// </summary>
    /// <param name="min">The lower bound, or null.</param>
    /// <param name="max">The upper bound, or null.</param>
    /// <returns><see langword="true"/> if the pair is ordered.</returns>
    internal static bool AreBoundsOrdered(int? min, int? max) =>
        min is null || max is null || min.Value <= max.Value;

    /// <summary>
    /// Creates a filter passing rows whose <see cref="IDecisionFilterData.MoveNumber"/>
    /// is in <c>[min, max]</c> inclusive AND whose game started from the canonical
    /// opening position. Either bound may be null to leave that end open.
    /// </summary>
    /// <param name="min">Inclusive lower bound, or null for an open lower end.</param>
    /// <param name="max">Inclusive upper bound, or null for an open upper end.</param>
    /// <exception cref="ArgumentOutOfRangeException">
    /// A supplied bound is below one — see <see cref="IsBoundAtLeastOne"/>.
    /// </exception>
    /// <exception cref="ArgumentException">
    /// Both bounds are supplied and <paramref name="min"/> exceeds
    /// <paramref name="max"/> (an empty range that could never match) — see
    /// <see cref="AreBoundsOrdered"/>.
    /// </exception>
    public MoveNumberFilter(int? min = null, int? max = null)
    {
        if (!IsBoundAtLeastOne(min))
            throw new ArgumentOutOfRangeException(
                nameof(min), min, AtLeastOneBoundMessage);

        if (!IsBoundAtLeastOne(max))
            throw new ArgumentOutOfRangeException(
                nameof(max), max, AtLeastOneBoundMessage);

        if (!AreBoundsOrdered(min, max))
            throw new ArgumentException(
                $"Min ({min}) must not exceed Max ({max}) (an empty move-number range).", nameof(min));

        _min = min;
        _max = max;
    }

    /// <summary>
    /// The rejection text shared by both bound checks, so the two read
    /// identically whichever end the user got wrong.
    /// </summary>
    private const string AtLeastOneBoundMessage =
        "Bound must be one or greater: move numbers within a game are 1-based.";

    /// <inheritdoc/>
    public bool Matches(IDecisionFilterData data)
    {
        if (!data.IsStandardStart) return false;
        return (_min is null || data.MoveNumber >= _min.Value) &&
               (_max is null || data.MoveNumber <= _max.Value);
    }

    /// <summary>
    /// Mid-stream early exit: once a decision past <c>max</c> is seen, no
    /// later decision in the same game can match, since move numbers
    /// increase monotonically. Does not need to check
    /// <see cref="IDecisionFilterData.IsStandardStart"/>: non-standard games
    /// are skipped by <see cref="ShouldSkipGame"/> before any rows reach the
    /// row-level pipeline.
    /// </summary>
    public bool ShouldAdvanceGame(IDecisionFilterData data) =>
        _max is int max && data.MoveNumber > max;

    /// <inheritdoc/>
    public bool ShouldSkipMatch(IMatchInfo match) => false;

    /// <inheritdoc/>
    public bool ShouldSkipGame(IGameInfo game) => !game.IsStandardStart;
}
