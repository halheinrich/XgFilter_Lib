using BgDataTypes_Lib;

namespace XgFilter_Lib.Filtering;

/// <summary>
/// The name facet and the error facet, as one filter for the pair: it states
/// once which half of a decision each reads (SPEC-filtering §8,
/// halheinrich/backgammon#440). A cube record holds two decisions — the
/// doubler's, and, where a double was offered, the taker's — and a checker
/// play one, the player's. The two facets cannot be two filters ANDed: "some
/// listed player's half has an error in range" is not "some listed player's
/// half" AND "the doubler's error in range".
///
/// <para>
/// <b>The rule.</b>
/// </para>
/// <list type="bullet">
/// <item><description>
/// <b>The selected halves.</b> With no names listed, the doubler's half:
/// <see cref="IDecisionFilterData.PlayerResult"/>, which for a checker play is
/// the player's. With names listed, each half whose maker is listed, compared
/// case-insensitively: <see cref="IDecisionFilterData.Player"/> selects
/// <see cref="IDecisionFilterData.PlayerResult"/>, and
/// <see cref="IDecisionFilterData.Taker"/> selects
/// <see cref="IDecisionFilterData.TakerResult"/> where that is not
/// <see langword="null"/>. A record with no recorded name selects nothing.
/// </description></item>
/// <item><description>
/// <b>A half qualifies</b> when it is selected and, with bounds set, its
/// result has an error (<see cref="PlayerResult.TryGetError"/>) within
/// <c>[min, max]</c>. With no bounds, every selected half qualifies.
/// </description></item>
/// <item><description>
/// <see cref="Matches"/> is true when some half qualifies, and
/// <see cref="QualifyingError"/> is the largest error among the qualifying
/// halves.
/// </description></item>
/// </list>
///
/// <para>
/// So the name facet admits a listed taker's take or pass, never the
/// opponent's no-double (no double was offered, so there is no taker half);
/// a further name on a list that holds some cannot remove a decision, though
/// the first can (with no bounds, an empty list admits every player's
/// decision and one name excludes the others); and an error range judges the
/// listed player's half, not the unlisted doubler's. A double offered and
/// never answered (<see cref="PlayerResultKind.NotRecorded"/>) passes the name
/// facet for the listed taker and, having no error, never satisfies an active
/// range through that half.
/// </para>
///
/// <para>
/// <b>Only a result with an error satisfies a range</b> (SPEC-scoring §2a): a
/// scored move (<see cref="PlayerResultKind.Scored"/>) with its error, and a
/// move the record does not state (<see cref="PlayerResultKind.Unstated"/>)
/// with the analyser's. A move the ranking does not score
/// (<see cref="PlayerResultKind.NotScored"/>, under depth first) has no
/// error, nor does a decision with no move recorded
/// (<see cref="PlayerResultKind.NotRecorded"/>). Which cases carry an error
/// is the producer's to say — <see cref="PlayerResult.TryGetError"/> is read,
/// never restated here.
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
/// Bounds are constrained: each must be a finite number of zero or greater,
/// a null end is open, and <c>min</c> must not exceed <c>max</c>. A bound
/// breaking the rule is a construction error rather than a range that
/// silently matches something other than what was set — the same posture
/// <see cref="Patterns.CheckerRange"/> takes towards a wrong-signed borne-off
/// bound. <see cref="IsBoundFiniteNonNegative"/> states the policy and why it
/// is a policy about bounds, not a claim about errors (an unstated error may
/// be signed); it and <see cref="AreBoundsOrdered"/> state the rule once for
/// the whole library; this constructor enforces it and
/// <see cref="FilterConfig.GetInvalidFields"/> reports it, so a consumer can
/// ask before it builds and the two answers cannot disagree.
/// </para>
/// </summary>
internal sealed class PlayerErrorFilter : IDecisionFilter, IMatchFilter
{
    private readonly HashSet<string> _players;
    private readonly double? _min;
    private readonly double? _max;

    /// <summary>
    /// The rule with no facet active — no names, no bounds: every decision
    /// matches, and <see cref="QualifyingError"/> is the doubler's error, or
    /// <see langword="null"/>. What <see cref="DecisionFilterSet.ErrorRead"/>
    /// answers for a set that holds no filter for the pair.
    /// </summary>
    public static PlayerErrorFilter Default { get; } = new([]);

    /// <summary>
    /// Half of the error facet's bound rule, and its single statement: an
    /// error bound must be a finite number of zero or greater. An absent bound
    /// (null) satisfies it vacuously — the rule constrains values, never
    /// presence, and a null end stays open — and zero satisfies it outright,
    /// an exact-zero error filter being meaningful.
    /// <para>
    /// <b>A policy about bounds, not a fact about errors.</b> A scored error
    /// is never negative (<see cref="PlayerResult.Scored"/> refuses one), but
    /// an unstated error is the analyser's figure as the record states it,
    /// and <see cref="PlayerResult.Unstated"/> holds no sign rule, so an
    /// unstated error may be signed. The rule does not rest on errors being
    /// non-negative: a bound below zero is refused because the accepted
    /// policy says so, whatever errors the records hold.
    /// </para>
    /// <para>
    /// <b>Finite</b> rules out all three non-finite values, and each is the
    /// intended verdict rather than an accident of the comparison.
    /// <see cref="double.NaN"/> compares false against everything, so a NaN
    /// bound admits nothing — exactly the failure mode the rule exists to
    /// catch. Positive infinity admits nothing as a lower bound, and as an
    /// upper bound is a second spelling of the open end, which null already
    /// spells (halheinrich/backgammon#374). Negative infinity is below zero
    /// besides. All three are reachable: <c>double.TryParse</c> accepts the
    /// literals "NaN" and "Infinity", and reads an out-of-range literal such as
    /// "1e999" as infinity, so a text-entry consumer can produce any of them.
    /// </para>
    /// <para>
    /// <b>There is no upper cap</b> (halheinrich/backgammon#374): any finite
    /// bound of zero or greater is admissible, however large. A bound above
    /// every error the analyser reports is loose, not wrong — it admits what a
    /// smaller one would — and the library does not guess at a ceiling.
    /// </para>
    /// </summary>
    /// <param name="bound">The bound to judge, or null for an open end.</param>
    /// <returns><see langword="true"/> if <paramref name="bound"/> is admissible.</returns>
    internal static bool IsBoundFiniteNonNegative(double? bound) =>
        bound is null || (double.IsFinite(bound.Value) && bound.Value >= 0);

    /// <summary>
    /// The other half of the error facet's bound rule, and its single
    /// statement: when both bounds are present the lower must not exceed the
    /// upper. A one-sided or absent range satisfies it vacuously, and an equal
    /// pair satisfies it (the bounds being inclusive, that is the exact-value
    /// filter).
    /// <para>
    /// This is a rule about the <em>pair</em>, so a violation blames neither
    /// bound alone — see <see cref="FilterConfig.GetInvalidFields"/>, which
    /// reports both. It presumes bounds already admissible under
    /// <see cref="IsBoundFiniteNonNegative"/>; against an inadmissible one its verdict
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
    /// Creates the filter for the pair from the listed names and the error
    /// bounds. An empty <paramref name="players"/> lists no names (the doubler's
    /// half is read); both bounds null set no range (every selected half
    /// qualifies). Names compare case-insensitively.
    /// </summary>
    /// <param name="players">The listed names; empty for none.</param>
    /// <param name="min">Inclusive lower bound on the error, or null for an open lower end.</param>
    /// <param name="max">Inclusive upper bound on the error, or null for an open upper end.</param>
    /// <exception cref="ArgumentNullException"><paramref name="players"/> is null.</exception>
    /// <exception cref="ArgumentOutOfRangeException">
    /// A supplied bound is not a finite number of zero or greater — negative,
    /// infinite, or <see cref="double.NaN"/>; see
    /// <see cref="IsBoundFiniteNonNegative"/>.
    /// </exception>
    /// <exception cref="ArgumentException">
    /// Both bounds are supplied and <paramref name="min"/> exceeds
    /// <paramref name="max"/> (an empty range that could never match) — see
    /// <see cref="AreBoundsOrdered"/>.
    /// </exception>
    public PlayerErrorFilter(IEnumerable<string> players, double? min = null, double? max = null)
    {
        ArgumentNullException.ThrowIfNull(players);

        if (!IsBoundFiniteNonNegative(min))
            throw new ArgumentOutOfRangeException(
                nameof(min), min, FiniteNonNegativeBoundMessage);

        if (!IsBoundFiniteNonNegative(max))
            throw new ArgumentOutOfRangeException(
                nameof(max), max, FiniteNonNegativeBoundMessage);

        if (!AreBoundsOrdered(min, max))
            throw new ArgumentException(
                $"Min ({min}) must not exceed Max ({max}) (an empty error range).", nameof(min));

        _players = new HashSet<string>(players, StringComparer.OrdinalIgnoreCase);
        _min = min;
        _max = max;
    }

    /// <summary>
    /// The rejection text shared by both bound checks, so the two read
    /// identically whichever end the user got wrong.
    /// </summary>
    private const string FiniteNonNegativeBoundMessage =
        "Bound must be a finite number of zero or greater.";

    private bool NamesListed => _players.Count > 0;

    private bool BoundsSet => _min is not null || _max is not null;

    /// <summary>
    /// True when some selected half of <paramref name="data"/> qualifies (see
    /// the rule on the type).
    /// </summary>
    public bool Matches(IDecisionFilterData data) => Evaluate(data).Qualifies;

    /// <summary>
    /// The largest error among the halves of <paramref name="data"/> that
    /// qualify, or <see langword="null"/> when none has one — none qualifies,
    /// or none that does carries an error. The error this configuration read on
    /// the decision, so the by-error order reads the half the facets passed:
    /// with bounds <c>[0, 0.1]</c>, a listed taker at 0.2 and a listed doubler
    /// at 0.05, the decision passes on the doubler's half and this is 0.05.
    /// Defined for every decision, passing or not.
    /// </summary>
    public double? QualifyingError(IDecisionFilterData data) => Evaluate(data).Error;

    /// <summary>
    /// The rule, stated once for <see cref="Matches"/> and
    /// <see cref="QualifyingError"/>: each selected half considered in turn.
    /// </summary>
    private (bool Qualifies, double? Error) Evaluate(IDecisionFilterData data)
    {
        bool qualifies = false;
        double? largest = null;

        if (!NamesListed || IsListed(data.Player))
            Consider(data.PlayerResult, ref qualifies, ref largest);

        if (NamesListed && IsListed(data.Taker) && data.TakerResult is { } taker)
            Consider(taker, ref qualifies, ref largest);

        return (qualifies, largest);
    }

    private bool IsListed(string? name) => name is not null && _players.Contains(name);

    /// <summary>
    /// Folds one selected half into the verdict: it qualifies with no bounds
    /// set, or with an error in range, and a qualifying half's error competes
    /// for the largest.
    /// </summary>
    private void Consider(PlayerResult half, ref bool qualifies, ref double? largest)
    {
        bool hasError = half.TryGetError(out double error);
        if (BoundsSet && !(hasError && InRange(error)))
            return;

        qualifies = true;
        if (hasError && (largest is null || error > largest.Value))
            largest = error;
    }

    private bool InRange(double error) =>
        (_min is null || error >= _min.Value) && (_max is null || error <= _max.Value);

    /// <summary>
    /// With names listed, skip the match if neither player is listed: every
    /// half's maker — the player on roll and the taker alike — is one of the
    /// header's two, so none can then be selected (the skip guarantee on
    /// <see cref="IMatchFilter"/>). Compared the way <see cref="Matches"/>
    /// compares, case-insensitively. With no names listed the doubler's half
    /// of every decision is selected, so the filter never votes to skip.
    /// </summary>
    public bool ShouldSkipMatch(IMatchInfo match) =>
        NamesListed && !_players.Contains(match.Player1) && !_players.Contains(match.Player2);

    /// <summary>
    /// Player names do not change per game — no game-level skip.
    /// </summary>
    public bool ShouldSkipGame(IGameInfo game) => false;
}
