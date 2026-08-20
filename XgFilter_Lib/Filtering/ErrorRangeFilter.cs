using BgDataTypes_Lib;

namespace XgFilter_Lib.Filtering;

/// <summary>
/// Passes rows where <see cref="IDecisionFilterData.FilterError"/> falls within
/// [min, max] (inclusive). Either bound may be omitted (null) to leave that end open.
/// Rows with a null <c>FilterError</c> (unanalysed positions) do not pass.
///
/// <para>
/// Bounds are constrained, because the quantity filtered is a magnitude:
/// <see cref="IDecisionFilterData.FilterError"/> is documented as an error
/// magnitude (≥ 0), so a negative lower bound is a no-op dressed as a filter and
/// a negative upper bound admits nothing at all, while <c>min &gt; max</c> is
/// empty by construction. None of the three is a filter a user could mean, so
/// each is a construction error rather than a range that silently never matches
/// — the same posture <see cref="Patterns.CheckerRange"/> takes towards a
/// wrong-signed borne-off bound. <see cref="IsBoundNonNegative"/> and
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
    /// Creates a filter passing rows whose <see cref="IDecisionFilterData.FilterError"/>
    /// is in <c>[min, max]</c> inclusive. Either bound may be null to leave that end open.
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
        "Bound must be a real number of zero or greater: filter error is a magnitude.";

    /// <inheritdoc/>
    public bool Matches(IDecisionFilterData data)
    {
        if (data.FilterError is not double error) return false;
        return (_min is null || error >= _min.Value) &&
               (_max is null || error <= _max.Value);
    }
}
