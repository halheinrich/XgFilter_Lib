using BgDataTypes_Lib;

namespace XgFilter_Lib.Filtering;

/// <summary>
/// Passes decisions whose player erred by an amount within [min, max]
/// (inclusive): the error of the player's result
/// (<see cref="IDecisionFilterData.PlayerResult"/>) under the ranking the
/// view was built for (<see cref="IDecisionFilterData.Ranking"/>). Either bound
/// may be omitted (null) to leave that end open.
///
/// <para>
/// <b>Only a result with an error passes</b> (SPEC-scoring §2a): a scored move
/// (<see cref="PlayerResultKind.Scored"/>) with its error, and a move the
/// record does not state (<see cref="PlayerResultKind.Unstated"/>) with the
/// analyser's. A move the ranking does not score
/// (<see cref="PlayerResultKind.NotScored"/>, under depth first) has no error
/// and never passes, however the bounds are set; nor does a decision with no
/// move recorded (<see cref="PlayerResultKind.NotRecorded"/>). Which cases
/// carry an error is the producer's to say — <see cref="PlayerResult.TryGetError"/>
/// is read, never restated here.
/// </para>
///
/// <para>
/// <b>The filter holds no ranking.</b> A view or a row is built for one
/// ranking and carries it, so "erred by more than x" under a ranking is this
/// filter over views, or rows, built for that ranking — the caller states it
/// where it builds them (<see cref="FilteredDecisionIterator"/>, or
/// <see cref="BgDecisionData.ViewFor"/> directly).
/// </para>
///
/// <para>
/// Bounds are constrained, because the quantity filtered is a magnitude — a
/// scored error is never negative (<see cref="PlayerResult.Scored"/> refuses
/// one) — so a negative lower bound is a no-op dressed as a filter and a
/// negative upper bound admits nothing at all, while <c>min &gt; max</c> is
/// empty by construction. None of the three is a filter a user could mean, so
/// each is a construction error rather than a range that silently never
/// matches — the same posture <see cref="Patterns.CheckerRange"/> takes towards
/// a wrong-signed borne-off bound. <see cref="IsBoundNonNegative"/> and
/// <see cref="AreBoundsOrdered"/> state that rule once for the whole library;
/// this constructor enforces it and
/// <see cref="FilterConfig.GetInvalidFields"/> reports it, so a consumer can
/// ask before it builds and the two answers cannot disagree.
/// </para>
/// </summary>
internal sealed class ErrorRangeFilter : IDecisionFilter
{
    private readonly double? _min;
    private readonly double? _max;

    /// <summary>
    /// Half of the facet's bound rule, and its single statement: an error bound
    /// must be zero or greater. An absent bound (null) satisfies it vacuously —
    /// the rule constrains values, never presence — and zero satisfies it
    /// outright, an exact-zero error filter being meaningful.
    /// <para>
    /// Stated as <c>value &gt;= 0</c> rather than <c>!(value &lt; 0)</c> so that
    /// <see cref="double.NaN"/>, which compares false against everything, is
    /// rejected too. That is the intended verdict and not an accident of the
    /// comparison: a NaN bound admits nothing, exactly the failure mode the rule
    /// exists to catch, and it is reachable — <c>double.TryParse</c> accepts the
    /// literal "NaN", so a text-entry consumer can produce one. Positive
    /// infinity is accepted, being merely a very large finite bound's limit and
    /// no more empty than one.
    /// </para>
    /// </summary>
    /// <param name="bound">The bound to judge, or null for an open end.</param>
    /// <returns><see langword="true"/> if <paramref name="bound"/> is admissible.</returns>
    internal static bool IsBoundNonNegative(double? bound) => bound is null || bound.Value >= 0;

    /// <summary>
    /// The other half of the facet's bound rule, and its single statement: when
    /// both bounds are present the lower must not exceed the upper. A one-sided
    /// or absent range satisfies it vacuously, and an equal pair satisfies it
    /// (the bounds being inclusive, that is the exact-value filter).
    /// <para>
    /// This is a rule about the <em>pair</em>, so a violation blames neither
    /// bound alone — see <see cref="FilterConfig.GetInvalidFields"/>, which
    /// reports both. It presumes bounds already admissible under
    /// <see cref="IsBoundNonNegative"/>; against an inadmissible one its verdict
    /// is a restatement of that fault rather than news, which is why the
    /// constructor checks the bounds individually first.
    /// </para>
    /// </summary>
    /// <param name="min">The lower bound, or null.</param>
    /// <param name="max">The upper bound, or null.</param>
    /// <returns><see langword="true"/> if the pair is ordered.</returns>
    internal static bool AreBoundsOrdered(double? min, double? max) =>
        min is null || max is null || min.Value <= max.Value;

    /// <summary>
    /// Creates a filter passing decisions whose player's error
    /// (<see cref="IDecisionFilterData.PlayerResult"/>) is in <c>[min, max]</c>
    /// inclusive. Either bound may be null to leave that end open.
    /// </summary>
    /// <param name="min">Inclusive lower bound, or null for an open lower end.</param>
    /// <param name="max">Inclusive upper bound, or null for an open upper end.</param>
    /// <exception cref="ArgumentOutOfRangeException">
    /// A supplied bound is negative or <see cref="double.NaN"/> — see
    /// <see cref="IsBoundNonNegative"/>.
    /// </exception>
    /// <exception cref="ArgumentException">
    /// Both bounds are supplied and <paramref name="min"/> exceeds
    /// <paramref name="max"/> (an empty range that could never match) — see
    /// <see cref="AreBoundsOrdered"/>.
    /// </exception>
    public ErrorRangeFilter(double? min = null, double? max = null)
    {
        if (!IsBoundNonNegative(min))
            throw new ArgumentOutOfRangeException(
                nameof(min), min, NonNegativeBoundMessage);

        if (!IsBoundNonNegative(max))
            throw new ArgumentOutOfRangeException(
                nameof(max), max, NonNegativeBoundMessage);

        if (!AreBoundsOrdered(min, max))
            throw new ArgumentException(
                $"Min ({min}) must not exceed Max ({max}) (an empty error range).", nameof(min));

        _min = min;
        _max = max;
    }

    /// <summary>
    /// The rejection text shared by both bound checks, so the two read
    /// identically whichever end the user got wrong.
    /// </summary>
    private const string NonNegativeBoundMessage =
        "Bound must be a real number of zero or greater: a player's error is a magnitude.";

    /// <summary>
    /// Returns <see langword="true"/> iff the player's result under the view's
    /// ranking has an error (<see cref="PlayerResult.TryGetError"/>) and that
    /// error lies within the bounds. A result with no error — not scored, or
    /// not recorded — never passes.
    /// </summary>
    public bool Matches(IDecisionFilterData data) =>
        data.PlayerResult.TryGetError(out double error)
        && (_min is null || error >= _min.Value)
        && (_max is null || error <= _max.Value);
}
