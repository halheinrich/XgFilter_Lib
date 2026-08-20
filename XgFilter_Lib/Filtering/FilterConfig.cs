using System.Text.Json;
using System.Text.Json.Serialization;
using BgDataTypes_Lib;
using XgFilter_Lib.Enums;
using XgFilter_Lib.Patterns;

namespace XgFilter_Lib.Filtering;

/// <summary>
/// Serializable configuration for a <see cref="DecisionFilterSet"/>.
/// Wire-friendly (default-constructible, mutable, JSON-round-trippable
/// with <c>JsonStringEnumConverter</c>) so consumers can bind it to UI
/// state on one side and POST it across a process boundary to be
/// materialized on the other.
///
/// <para>
/// Empty-list semantics (matches what consumer-side glue used to do
/// before this type existed): an empty <see cref="Players"/>,
/// <see cref="MatchScores"/>, <see cref="ContactTypes"/>,
/// <see cref="PositionTypes"/>, <see cref="PlayTypes"/>, or
/// <see cref="DiceRolls"/> means "no
/// filter of this kind is active" — not "reject everything."
/// A null or empty <see cref="PositionPattern"/> means the same for the
/// per-location pattern filter. The depth facet is inactive — and so passes
/// everything — iff all three of its mode toggles
/// (<see cref="IncludeEvaluations"/>, <see cref="IncludeRollouts"/>,
/// <see cref="IncludeBookRollouts"/>) are off; a level list whose toggle is
/// off is inert and neither activates nor constrains anything. See
/// <see cref="Build"/> for the derivation.
/// <see cref="Build"/> simply skips adding the filter to the set in that
/// case. <see cref="DecisionType"/>
/// defaults to <see cref="DecisionTypeOption.Both"/>, which is a
/// no-op in <see cref="DecisionTypeFilter"/>.
/// </para>
///
/// <para>
/// Range filters (<see cref="ErrorRangeFilter"/>,
/// <see cref="MoveNumberFilter"/>) are added if either bound is set;
/// both-null pairs are skipped.
/// </para>
///
/// <para>
/// Validity is separate from activity, and is a query rather than a gate on
/// assignment. Every setter accepts whatever it is given — a stored document
/// written before a rule existed must still load, so that a consumer can show
/// the offending value and let the user fix it — and
/// <see cref="GetInvalidFields"/> is where the rules are asked about. See it
/// for the posture and <see cref="Build"/> for what happens to a configuration
/// that was built anyway.
/// </para>
///
/// <para>
/// Equality: value-based over every member — a config's identity is its
/// content, so two configs describing the same filtering are equal whoever
/// built them. That is what lets a consumer tell "the user changed something"
/// from "the user is back where they started" without re-materializing or
/// serializing anything. See <see cref="Equals(FilterConfig)"/> for the
/// per-member rules (the list members compare order-insensitively). No
/// <c>==</c> / <c>!=</c> operators are declared: this is a mutable reference
/// type, and by the prevailing convention its operators keep reference
/// semantics — call <see cref="Equals(FilterConfig)"/> for value comparison.
/// The standard caveat for a mutable type with value equality applies: an
/// instance must not be mutated while it is in use as a key in a hash-based
/// collection, since its hash would change underneath the collection. Nothing
/// here does that today; this line is the guard against a consumer starting to.
/// </para>
/// </summary>
public sealed class FilterConfig : IEquatable<FilterConfig>
{
    /// <summary>Player names whose decisions should pass; empty = no player filter.</summary>
    public IList<string> Players { get; set; } = new List<string>();

    /// <summary>Which decision types to admit. Defaults to <see cref="DecisionTypeOption.Both"/>.</summary>
    public DecisionTypeOption DecisionType { get; set; } = DecisionTypeOption.Both;

    /// <summary>
    /// Match-score tokens to admit (e.g. <c>"3a5a"</c>, <c>"1a5aC"</c>,
    /// <c>"money"</c>). Empty = no score filter.
    /// </summary>
    public IList<string> MatchScores { get; set; } = new List<string>();

    /// <summary>
    /// Inclusive lower bound on filter-error; null = open lower bound. Must be
    /// zero or greater, and must not exceed <see cref="ErrorMax"/> when both
    /// are present — the setter does not enforce that, <see cref="GetInvalidFields"/>
    /// reports it, and <see cref="Build"/> rejects it.
    /// </summary>
    public double? ErrorMin { get; set; }

    /// <summary>
    /// Inclusive upper bound on filter-error; null = open upper bound. Must be
    /// zero or greater, and must not fall below <see cref="ErrorMin"/> when both
    /// are present — the setter does not enforce that, <see cref="GetInvalidFields"/>
    /// reports it, and <see cref="Build"/> rejects it.
    /// </summary>
    public double? ErrorMax { get; set; }

    /// <summary>Inclusive lower bound on move number; null = open lower bound.</summary>
    public int? MoveNumberMin { get; set; }

    /// <summary>Inclusive upper bound on move number; null = open upper bound.</summary>
    public int? MoveNumberMax { get; set; }

    /// <summary>
    /// Contact types to admit (<see cref="ContactType.Contact"/> /
    /// <see cref="ContactType.Race"/>; OR semantics). Empty = no contact-type
    /// filter. A separate axis from <see cref="PositionTypes"/>, with which it
    /// composes via AND.
    /// </summary>
    public IList<ContactType> ContactTypes { get; set; } = new List<ContactType>();

    /// <summary>Position types to admit (OR semantics). Empty = no position-type filter.</summary>
    public IList<PositionType> PositionTypes { get; set; } = new List<PositionType>();

    /// <summary>Play types to admit (OR semantics). Empty = no play-type filter.</summary>
    public IList<PlayType> PlayTypes { get; set; } = new List<PlayType>();

    /// <summary>
    /// Whether the depth facet admits plain evaluations
    /// (<see cref="AnalysisMode.Evaluation"/>), qualified by
    /// <see cref="EvaluationLevels"/>. One of the three independent per-mode
    /// toggles; the facet is a union across the enabled toggles — see
    /// <see cref="Build"/> and <see cref="AnalysisDepthFilter"/>.
    /// </summary>
    public bool IncludeEvaluations { get; set; }

    /// <summary>
    /// Checked levels qualifying <see cref="IncludeEvaluations"/> (OR
    /// semantics) — the level of the evaluation itself. Empty = any level.
    /// Inert while <see cref="IncludeEvaluations"/> is off: a level selection
    /// qualifies only its own mode, never the other clauses.
    /// </summary>
    public IList<AnalysisLevel> EvaluationLevels { get; set; } = new List<AnalysisLevel>();

    /// <summary>
    /// Whether the depth facet admits full rollouts
    /// (<see cref="AnalysisMode.Rollout"/>), qualified by
    /// <see cref="RolloutLevels"/>. One of the three independent per-mode
    /// toggles; see <see cref="Build"/>.
    /// </summary>
    public bool IncludeRollouts { get; set; }

    /// <summary>
    /// Checked levels qualifying <see cref="IncludeRollouts"/> (OR semantics)
    /// — the <em>inner</em> level of the rollout's games, not the level of a
    /// direct evaluation (checker rollouts never carry Roller-family inner
    /// levels). Empty = any inner level, the common selection. Inert while
    /// <see cref="IncludeRollouts"/> is off.
    /// </summary>
    public IList<AnalysisLevel> RolloutLevels { get; set; } = new List<AnalysisLevel>();

    /// <summary>
    /// Whether the depth facet admits opening-book hits
    /// (<see cref="AnalysisMode.BookRollout"/>), qualified by
    /// <see cref="BookRolloutLevels"/>. Selecting this with no level checked
    /// is the path by which an unenriched book hit
    /// (<see cref="AnalysisMode.BookRollout"/> + <see cref="AnalysisLevel.Unknown"/>)
    /// is admitted — see <see cref="Build"/>.
    /// </summary>
    public bool IncludeBookRollouts { get; set; }

    /// <summary>
    /// Checked levels qualifying <see cref="IncludeBookRollouts"/> (OR
    /// semantics) — the book entry's inner level. Empty = any level,
    /// including the <see cref="AnalysisLevel.Unknown"/> an unenriched book
    /// hit carries. Inert while <see cref="IncludeBookRollouts"/> is off.
    /// </summary>
    public IList<AnalysisLevel> BookRolloutLevels { get; set; } = new List<AnalysisLevel>();

    /// <summary>
    /// Dice rolls to admit (OR semantics). Empty = no dice filter. Each roll is
    /// canonical and unordered, so listing <c>"31"</c> admits both dice orders;
    /// cube decisions (which carry no roll) never pass an active dice filter.
    /// Serializes as a string-token array (e.g. <c>["31","66"]</c>) via the
    /// converter <see cref="DiceRoll"/> declares on itself, so it needs no
    /// converter registered in <see cref="CanonicalOptions"/> — the same
    /// self-describing case as the depth level lists (e.g.
    /// <see cref="EvaluationLevels"/>) and <see cref="PositionPattern"/>.
    /// </summary>
    public IList<DiceRoll> DiceRolls { get; set; } = new List<DiceRoll>();

    /// <summary>
    /// A general per-location checker-range constraint on the on-roll board.
    /// Null or empty = no pattern filter. Serializes as its bracket-list string
    /// via the converter <see cref="BoardPattern"/> declares on itself (see
    /// <see cref="BoardPatternJsonConverter"/>). Composes via AND with every
    /// other active filter, including <see cref="PositionTypes"/>.
    /// </summary>
    public BoardPattern? PositionPattern { get; set; }

    // -----------------------------------------------------------------------
    //  Facet rules — the single source of truth for facet activation
    // -----------------------------------------------------------------------

    /// <summary>
    /// One facet's rule pair: the activation predicate and the filter factory.
    /// <paramref name="CreateFilter"/> is invoked only when
    /// <paramref name="IsActive"/> holds, so a factory may rely on the
    /// predicate's guarantees (e.g. a non-null pattern).
    /// </summary>
    private sealed record FacetRule(
        FilterFacet Facet,
        Func<FilterConfig, bool> IsActive,
        Func<FilterConfig, IDecisionFilter> CreateFilter);

    /// <summary>
    /// The per-facet activation rules, in <see cref="Build"/>'s add order. This
    /// table is the single source of truth for "which facets does this config
    /// activate": <see cref="Build"/> materializes a filter for each active
    /// facet and <see cref="GetActiveFacets"/> reports the same predicates'
    /// verdicts, so the two surfaces cannot drift. Any new facet gate belongs
    /// here — never as an ad-hoc check in either consumer.
    /// </summary>
    private static readonly FacetRule[] FacetRules =
    [
        new(FilterFacet.Players,
            static c => c.Players.Count > 0,
            static c => new PlayerFilter(c.Players)),

        // The Both default is a no-op in DecisionTypeFilter; skipping the add
        // keeps the set's filter list lean.
        new(FilterFacet.DecisionType,
            static c => c.DecisionType != DecisionTypeOption.Both,
            static c => new DecisionTypeFilter(c.DecisionType)),

        new(FilterFacet.MatchScores,
            static c => c.MatchScores.Count > 0,
            static c => new MatchScoreFilter(c.MatchScores)),

        // Presence-based like every other row: a bound that violates the
        // facet's non-negative/ordered rule still activates the facet (it is a
        // filter the user set, however broken). The factory is where that rule
        // bites — see GetInvalidFields for asking before building.
        new(FilterFacet.ErrorRange,
            static c => c.ErrorMin.HasValue || c.ErrorMax.HasValue,
            static c => new ErrorRangeFilter(c.ErrorMin, c.ErrorMax)),

        new(FilterFacet.MoveNumberRange,
            static c => c.MoveNumberMin.HasValue || c.MoveNumberMax.HasValue,
            static c => new MoveNumberFilter(c.MoveNumberMin, c.MoveNumberMax)),

        new(FilterFacet.ContactTypes,
            static c => c.ContactTypes.Count > 0,
            static c => new ContactTypeFilter(c.ContactTypes)),

        new(FilterFacet.PositionTypes,
            static c => c.PositionTypes.Count > 0,
            static c => new PositionTypeFilter(c.PositionTypes)),

        new(FilterFacet.PlayTypes,
            static c => c.PlayTypes.Count > 0,
            static c => new PlayTypeFilter(c.PlayTypes)),

        // Depth facet (a union of per-mode clauses). Active iff any mode
        // toggle is on; a level list whose toggle is off is inert, so it
        // neither activates the facet nor constrains anything. The clause
        // derivation itself lives in CreateAnalysisDepthFilter.
        new(FilterFacet.AnalysisDepth,
            static c => c.IncludeEvaluations || c.IncludeRollouts || c.IncludeBookRollouts,
            static c => c.CreateAnalysisDepthFilter()),

        new(FilterFacet.DiceRolls,
            static c => c.DiceRolls.Count > 0,
            static c => new DiceRollFilter(c.DiceRolls)),

        // Null and the empty pattern are both inactive: an empty pattern
        // matches every board, so adding the filter would be a no-op AND step.
        new(FilterFacet.PositionPattern,
            static c => c.PositionPattern is { IsEmpty: false },
            static c => new PositionPatternFilter(c.PositionPattern!)),
    ];

    /// <summary>
    /// The depth facet's filter factory — the single source of truth for
    /// turning raw user intent (three per-mode toggles, each with its own
    /// level list) into the filter's clause union: one clause per enabled
    /// toggle, carrying that mode's level list verbatim (empty = any level,
    /// which <see cref="AnalysisDepthFilter"/> honours per clause). The panel
    /// supplies the six inputs verbatim and must not re-derive the clauses.
    /// A level list whose toggle is off contributes no clause — it is inert,
    /// never validated, and constrains nothing. Only called when the facet is
    /// active (some toggle on), so the clause union is never empty.
    /// </summary>
    private AnalysisDepthFilter CreateAnalysisDepthFilter()
    {
        var clauses = new List<AnalysisDepthFilter.Clause>();
        if (IncludeEvaluations)
            clauses.Add(new(AnalysisMode.Evaluation, EvaluationLevels));
        if (IncludeRollouts)
            clauses.Add(new(AnalysisMode.Rollout, RolloutLevels));
        if (IncludeBookRollouts)
            clauses.Add(new(AnalysisMode.BookRollout, BookRolloutLevels));
        return new AnalysisDepthFilter(clauses);
    }

    /// <summary>
    /// Materializes this configuration as a <see cref="DecisionFilterSet"/>.
    /// Each filter is added only when its corresponding configuration is
    /// non-empty / non-default — the per-facet gates live in the shared
    /// <see cref="FacetRules"/> table, which <see cref="GetActiveFacets"/>
    /// consults too; see the type-level remarks for the empty-list semantics.
    /// <para>
    /// Build is the point that rejects an invalid configuration, rather than
    /// materializing a filter nobody could have meant. A consumer that wants to
    /// ask first — to gate its own commit action and mark the offending input —
    /// asks <see cref="GetInvalidFields"/>; one that builds regardless gets an
    /// exception, never a silently empty result set. The rules themselves live
    /// on the filters, so the two paths cannot disagree.
    /// </para>
    /// </summary>
    /// <exception cref="ArgumentException">
    /// <see cref="MatchScores"/> contains a malformed token — see
    /// <see cref="MatchScoreFilter"/>; or <see cref="ErrorMin"/> exceeds
    /// <see cref="ErrorMax"/> with both present — see
    /// <see cref="ErrorRangeFilter.AreBoundsOrdered"/>.
    /// </exception>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <see cref="ContactTypes"/>, <see cref="PositionTypes"/>, or
    /// <see cref="PlayTypes"/> contains an undefined enum value — likewise
    /// <see cref="EvaluationLevels"/> / <see cref="RolloutLevels"/> /
    /// <see cref="BookRolloutLevels"/>, but only when the list's mode toggle
    /// is on (an inert level list is never validated). Also when
    /// <see cref="ErrorMin"/> or <see cref="ErrorMax"/> is negative or
    /// <see cref="double.NaN"/> — see
    /// <see cref="ErrorRangeFilter.IsBoundNonNegative"/>.
    /// </exception>
    public DecisionFilterSet Build()
    {
        var set = new DecisionFilterSet();

        foreach (var rule in FacetRules)
        {
            if (rule.IsActive(this))
                set.Add(rule.CreateFilter(this));
        }

        return set;
    }

    /// <summary>
    /// The facets this configuration would activate — exactly those for which
    /// <see cref="Build"/> would add a filter, judged by the same predicates
    /// (the shared <see cref="FacetRules"/> table), so the two cannot drift.
    /// Computed fresh from the current mutable state on each call; enumerates
    /// in <see cref="FilterFacet"/> declaration order, which is
    /// <see cref="Build"/>'s add order.
    /// </summary>
    /// <remarks>
    /// Activity is presence, not validity: the predicates count entries and
    /// check flags without validating contents, so a malformed
    /// <see cref="MatchScores"/> token — or an undefined enum value such as a
    /// corrupt <see cref="DecisionType"/> — still reports its facet active,
    /// while <see cref="Build"/> remains the point that throws. That is the
    /// posture a UI wants: garbage input is still an active filter to surface.
    /// Consequently this method never throws. <see cref="GetInvalidFields"/> is
    /// the companion query for the other axis — which values are wrong, as
    /// opposed to which facets are set.
    /// </remarks>
    /// <returns>
    /// The active facets as a set — distinct membership, O(1)
    /// <see cref="IReadOnlySet{T}.Contains"/>; empty for a default
    /// configuration.
    /// </returns>
    public IReadOnlySet<FilterFacet> GetActiveFacets()
    {
        var active = new SortedSet<FilterFacet>();

        foreach (var rule in FacetRules)
        {
            if (rule.IsActive(this))
                active.Add(rule.Facet);
        }

        return active;
    }

    // -----------------------------------------------------------------------
    //  Field rules — the single source of truth for per-field validity
    // -----------------------------------------------------------------------

    /// <summary>
    /// One field's validity rule: the field it can blame and the predicate that
    /// decides whether to. The predicate reports <em>violation</em>, so a rule
    /// that never fires leaves its field out of
    /// <see cref="GetInvalidFields"/>'s result.
    /// </summary>
    private sealed record FieldRule(
        FilterField Field,
        Func<FilterConfig, bool> IsViolated);

    /// <summary>
    /// The per-field validity rules, in <see cref="FilterField"/> declaration
    /// order. This table is the routing layer, not the rules: each predicate
    /// delegates to the owning filter, which is where a facet's semantics
    /// actually live, so the answer <see cref="GetInvalidFields"/> gives and the
    /// answer <see cref="Build"/> enforces are the same answer asked twice.
    ///
    /// <para>
    /// The table is sparse by design — only facets whose values can be filled
    /// in wrongly appear. A checkbox list has no rule to state (its failure mode
    /// is an undefined enum value, which no UI can produce and
    /// <see cref="Build"/> already rejects), so it contributes no row and its
    /// members are absent from <see cref="FilterField"/> entirely.
    /// </para>
    ///
    /// <para>
    /// The error facet contributes two rows for one rule pair, because a
    /// consumer marks inputs, not facets. Both rows consult the ordering rule
    /// via <see cref="ErrorBoundsMisordered"/>: <c>min &gt; max</c> is a fault
    /// of the pair, with neither bound wrong on its own, so it blames both and
    /// leaves the user to decide which end to move.
    /// </para>
    /// </summary>
    private static readonly FieldRule[] FieldRules =
    [
        new(FilterField.ErrorMin,
            static c => !ErrorRangeFilter.IsBoundNonNegative(c.ErrorMin)
                     || ErrorBoundsMisordered(c)),

        new(FilterField.ErrorMax,
            static c => !ErrorRangeFilter.IsBoundNonNegative(c.ErrorMax)
                     || ErrorBoundsMisordered(c)),
    ];

    /// <summary>
    /// Whether the error bounds break the ordering rule <em>blameably</em>:
    /// both individually admissible, and still out of order. The admissibility
    /// precondition is what keeps the blame honest — a bound already at fault
    /// for its own value drags the pair out of order as a side effect (a
    /// negative max is below any admissible min), and reporting that would red
    /// the field the user got right. Where the precondition fails the offending
    /// bound is already named by its own rule, so nothing goes unreported.
    /// </summary>
    private static bool ErrorBoundsMisordered(FilterConfig config) =>
        ErrorRangeFilter.IsBoundNonNegative(config.ErrorMin)
        && ErrorRangeFilter.IsBoundNonNegative(config.ErrorMax)
        && !ErrorRangeFilter.AreBoundsOrdered(config.ErrorMin, config.ErrorMax);

    /// <summary>
    /// The fields whose current values violate their facet's rule — the
    /// validity counterpart to <see cref="GetActiveFacets"/>, judged by the
    /// shared <see cref="FieldRules"/> table and computed fresh from the current
    /// mutable state on each call. Empty for a valid configuration, including a
    /// default one.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This is the query a consumer gates on. It is deliberately not a gate on
    /// assignment: the setters accept anything, so a document persisted before a
    /// rule existed still loads, and a half-typed value can sit in the
    /// configuration long enough to be shown back to the user. The consumer
    /// marks the fields named here, disables its commit action while any are
    /// named, and words the explanation itself — the same division of labour as
    /// <see cref="Patterns.BoardPattern.TryParse"/>, where the lib rules on the
    /// input and the consumer says so in its own voice. No message is offered
    /// here for that reason.
    /// </para>
    /// <para>
    /// Validity is orthogonal to activity: an inactive facet is never invalid
    /// (its rules constrain values, never presence), but an active one may be
    /// either. A field named here is one <see cref="Build"/> would throw on.
    /// The converse does not hold — an empty result is not a promise that
    /// <see cref="Build"/> succeeds, since the facets with no row in
    /// <see cref="FieldRules"/> still carry content <see cref="Build"/>
    /// validates (a malformed <see cref="MatchScores"/> token, an undefined enum
    /// value). Those join this query as their rules are stated.
    /// </para>
    /// <para>
    /// Never throws: judging a value is not using it.
    /// </para>
    /// </remarks>
    /// <returns>
    /// The invalid fields as a set — distinct membership, O(1)
    /// <see cref="IReadOnlySet{T}.Contains"/>, enumerating in
    /// <see cref="FilterField"/> declaration order.
    /// </returns>
    public IReadOnlySet<FilterField> GetInvalidFields()
    {
        var invalid = new SortedSet<FilterField>();

        foreach (var rule in FieldRules)
        {
            if (rule.IsViolated(this))
                invalid.Add(rule.Field);
        }

        return invalid;
    }

    // -----------------------------------------------------------------------
    //  Value equality — a config's identity is its content
    // -----------------------------------------------------------------------

    /// <summary>
    /// Value equality over every member of the configuration: the nine list
    /// facets, the four range bounds, <see cref="DecisionType"/>, the three
    /// depth-mode toggles, and <see cref="PositionPattern"/>. Two configs that
    /// compare equal describe exactly the same filtering.
    /// <para>
    /// Per-member rules. The list members compare <em>order-insensitively</em>,
    /// as multisets — selection order is not part of a config's meaning, so a
    /// re-ordered selection is not a change. (One accepted consequence of
    /// multiset rather than set semantics: a list holding the same entry twice
    /// differs from one holding it once, though both build the same filter.
    /// Duplicates are not reachable through a checkbox UI, and the direction of
    /// the error — reporting a difference that materializes identically — is
    /// the safe one for a consumer gating on "has anything changed?".) Strings
    /// compare ordinally. A null list member compares equal to an empty one:
    /// both mean "no filter of this kind," and equality stays total rather than
    /// throwing on a config an explicit JSON <c>null</c> produced.
    /// <see cref="PositionPattern"/> delegates to
    /// <see cref="BoardPattern.Equals(BoardPattern)"/>, so null and
    /// <see cref="BoardPattern.Empty"/> stay distinct — as they are everywhere
    /// else on this type.
    /// </para>
    /// </summary>
    /// <param name="other">The configuration to compare against, or null.</param>
    public bool Equals(FilterConfig? other)
    {
        if (ReferenceEquals(this, other))
            return true;

        if (other is null)
            return false;

        return DecisionType == other.DecisionType
            // Nullable<T>.Equals, not ==, so a NaN bound still equals itself
            // and equality stays reflexive across clones.
            && ErrorMin.Equals(other.ErrorMin)
            && ErrorMax.Equals(other.ErrorMax)
            && MoveNumberMin == other.MoveNumberMin
            && MoveNumberMax == other.MoveNumberMax
            && IncludeEvaluations == other.IncludeEvaluations
            && IncludeRollouts == other.IncludeRollouts
            && IncludeBookRollouts == other.IncludeBookRollouts
            && object.Equals(PositionPattern, other.PositionPattern)
            && SameContents(Players, other.Players)
            && SameContents(MatchScores, other.MatchScores)
            && SameContents(ContactTypes, other.ContactTypes)
            && SameContents(PositionTypes, other.PositionTypes)
            && SameContents(PlayTypes, other.PlayTypes)
            && SameContents(EvaluationLevels, other.EvaluationLevels)
            && SameContents(RolloutLevels, other.RolloutLevels)
            && SameContents(BookRolloutLevels, other.BookRolloutLevels)
            && SameContents(DiceRolls, other.DiceRolls);
    }

    /// <inheritdoc/>
    public override bool Equals(object? obj) => Equals(obj as FilterConfig);

    /// <summary>
    /// A hash consistent with <see cref="Equals(FilterConfig)"/> — including
    /// its order-insensitivity, so two configs whose selections are permutations
    /// of each other hash identically. See the type-level remarks for the
    /// mutation caveat this implies.
    /// </summary>
    public override int GetHashCode()
    {
        var hash = new HashCode();

        hash.Add(DecisionType);
        hash.Add(ErrorMin);
        hash.Add(ErrorMax);
        hash.Add(MoveNumberMin);
        hash.Add(MoveNumberMax);
        hash.Add(IncludeEvaluations);
        hash.Add(IncludeRollouts);
        hash.Add(IncludeBookRollouts);
        hash.Add(PositionPattern);

        hash.Add(ContentHash(Players));
        hash.Add(ContentHash(MatchScores));
        hash.Add(ContentHash(ContactTypes));
        hash.Add(ContentHash(PositionTypes));
        hash.Add(ContentHash(PlayTypes));
        hash.Add(ContentHash(EvaluationLevels));
        hash.Add(ContentHash(RolloutLevels));
        hash.Add(ContentHash(BookRolloutLevels));
        hash.Add(ContentHash(DiceRolls));

        return hash.ToHashCode();
    }

    /// <summary>
    /// Multiset comparison of two list facets — equal contents in any order.
    /// Tallies occurrences rather than sorting, so it asks only for equality and
    /// hashing of <typeparamref name="T"/> and serves the string, enum,
    /// <see cref="DiceRoll"/>, and <see cref="AnalysisLevel"/> facets alike; the
    /// default comparer is what makes the string facets ordinal. A null list is
    /// treated as empty, so equality never throws on one.
    /// </summary>
    private static bool SameContents<T>(IList<T>? a, IList<T>? b)
        where T : notnull
    {
        if (ReferenceEquals(a, b))
            return true;

        int count = a?.Count ?? 0;
        if (count != (b?.Count ?? 0))
            return false;

        if (count == 0)
            return true;

        // Null entries are tallied apart, because a Dictionary rejects a null
        // key. The notnull constraint is an annotation, not an enforcement: an
        // explicit null element in deserialized JSON still lands here.
        var remaining = new Dictionary<T, int>(count);
        int nulls = 0;
        foreach (var item in a!)
        {
            if (item is null)
                nulls++;
            else
                remaining[item] = remaining.TryGetValue(item, out int n) ? n + 1 : 1;
        }

        foreach (var item in b!)
        {
            if (item is null)
            {
                if (--nulls < 0)
                    return false;
            }
            else if (!remaining.TryGetValue(item, out int n))
            {
                return false;
            }
            else if (n == 1)
            {
                remaining.Remove(item);
            }
            else
            {
                remaining[item] = n - 1;
            }
        }

        // Equal counts, and every entry of b consumed a distinct entry of a, so
        // nothing of a can be left over.
        return true;
    }

    /// <summary>
    /// An order-independent content hash for one list facet, the hashing
    /// counterpart of <see cref="SameContents{T}(IList{T}, IList{T})"/>. XOR is
    /// what makes it order-independent; the count is folded in to separate lists
    /// that would otherwise cancel out. Null and empty share a hash because
    /// <see cref="SameContents{T}(IList{T}, IList{T})"/> calls them equal.
    /// </summary>
    private static int ContentHash<T>(IList<T>? items)
    {
        if (items is null || items.Count == 0)
            return 0;

        int aggregate = 0;
        foreach (var item in items)
            aggregate ^= item?.GetHashCode() ?? 0;

        return HashCode.Combine(items.Count, aggregate);
    }

    // -----------------------------------------------------------------------
    //  Canonical JSON serialization
    // -----------------------------------------------------------------------

    /// <summary>
    /// The single source of truth for how a <see cref="FilterConfig"/> maps to
    /// and from JSON. Registers <see cref="JsonStringEnumConverter"/> so the
    /// enum-typed members (<see cref="DecisionType"/>, <see cref="ContactTypes"/>,
    /// <see cref="PositionTypes"/>, <see cref="PlayTypes"/>) serialize as their
    /// declaration names rather than ordinals — none of those enum types carries a type-level
    /// <c>[JsonConverter]</c>, so without this they would round-trip as ints and
    /// silently rebind to the wrong member if the enum were ever reordered.
    /// <para>
    /// <see cref="PositionPattern"/> needs no converter registered here:
    /// <see cref="BoardPattern"/> carries its own
    /// <see cref="BoardPatternJsonConverter"/> via a type-level
    /// <see cref="JsonConverterAttribute"/>, so it round-trips as its
    /// bracket-list string under these options — and under any other
    /// <see cref="JsonSerializerOptions"/> a consumer might use across a wire.
    /// </para>
    /// <para>
    /// The three depth level lists (<see cref="EvaluationLevels"/> /
    /// <see cref="RolloutLevels"/> / <see cref="BookRolloutLevels"/>) and
    /// <see cref="DiceRolls"/> are the same self-describing case as
    /// <see cref="PositionPattern"/>: <see cref="AnalysisLevel"/> carries its own
    /// type-level <see cref="JsonStringEnumConverter"/> (it is owned by
    /// <c>BgDataTypes_Lib</c>, not <c>XgFilter_Lib.Enums</c>) and
    /// <see cref="DiceRoll"/> carries its own type-level
    /// <c>[JsonConverter]</c> (also owned by <c>BgDataTypes_Lib</c>), so each
    /// serializes as its string form even without the registration above —
    /// <see cref="AnalysisLevel"/> as its declaration name,
    /// <see cref="DiceRoll"/> as its two-digit token (<c>"31"</c>). The shared
    /// <see cref="JsonStringEnumConverter"/> registered here is redundant for
    /// the level lists (and does not touch <see cref="DiceRolls"/>)
    /// but harmless. <see cref="IncludeEvaluations"/> /
    /// <see cref="IncludeRollouts"/> / <see cref="IncludeBookRollouts"/> are
    /// plain booleans and need no converter.
    /// </para>
    /// Held as a cached, immutable instance: <see cref="JsonSerializerOptions"/>
    /// is expensive to build and thread-safe once first used.
    /// </summary>
    private static readonly JsonSerializerOptions CanonicalOptions = new()
    {
        Converters = { new JsonStringEnumConverter() },
    };

    /// <summary>
    /// Serializes this configuration to its canonical JSON representation —
    /// the inverse of <see cref="FromJson"/>. Enum values are written as their
    /// declaration names. This is the lib-owned wire format; consumers that
    /// persist or transmit a <see cref="FilterConfig"/> should round-trip
    /// through this pair rather than reaching for <see cref="JsonSerializer"/>
    /// directly, so the enum-as-string contract stays in one place.
    /// </summary>
    /// <returns>A JSON object string carrying every field of this instance.</returns>
    public string ToJson() => JsonSerializer.Serialize(this, CanonicalOptions);

    /// <summary>
    /// Deserializes a <see cref="FilterConfig"/> from its canonical JSON
    /// representation — the inverse of <see cref="ToJson"/>. Members absent
    /// from the JSON retain their type defaults (e.g. omitted lists materialize
    /// empty, omitted <see cref="DecisionType"/> stays
    /// <see cref="DecisionTypeOption.Both"/>), so a default-config blob and an
    /// empty object both round-trip to an equivalent instance.
    /// </summary>
    /// <param name="json">A JSON object string, typically produced by <see cref="ToJson"/>.</param>
    /// <returns>The materialized configuration.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="json"/> is null.</exception>
    /// <exception cref="ArgumentException">
    /// <paramref name="json"/> is the literal <c>null</c> token, which yields no
    /// configuration.
    /// </exception>
    /// <exception cref="JsonException"><paramref name="json"/> is malformed.</exception>
    public static FilterConfig FromJson(string json)
    {
        ArgumentNullException.ThrowIfNull(json);

        return JsonSerializer.Deserialize<FilterConfig>(json, CanonicalOptions)
            ?? throw new ArgumentException(
                "JSON deserialized to a null configuration; expected a FilterConfig object.",
                nameof(json));
    }

    /// <summary>
    /// Non-throwing counterpart to <see cref="FromJson"/>, following the
    /// <c>Parse</c>/<c>TryParse</c> convention. Absorbs the three ways a
    /// restore can fail — a null <paramref name="json"/> (e.g. a storage key
    /// that was never written), the literal <c>null</c> token, or malformed
    /// JSON — and yields a fresh default <see cref="FilterConfig"/> in each
    /// case. This single-sources the "absent or corrupt input restores to
    /// defaults" policy in the lib so consumers need no knowledge of the JSON
    /// representation or its exception taxonomy.
    /// </summary>
    /// <param name="json">
    /// The candidate JSON, or null. Typically read straight from a persistence
    /// store whose contents the caller does not control.
    /// </param>
    /// <param name="config">
    /// On return, always a usable configuration: the restored instance on
    /// success, or a fresh default on failure. A caller content with
    /// default-on-failure may ignore the return value and read
    /// <paramref name="config"/> directly.
    /// </param>
    /// <returns>
    /// <see langword="true"/> if <paramref name="json"/> was successfully
    /// deserialized; <see langword="false"/> if it was null, the <c>null</c>
    /// token, or malformed. A false result lets a caller react to a failed
    /// restore (e.g. clear the corrupt entry or record telemetry) without
    /// catching exceptions.
    /// </returns>
    public static bool TryFromJson(string? json, out FilterConfig config)
    {
        if (json is not null)
        {
            try
            {
                if (JsonSerializer.Deserialize<FilterConfig>(json, CanonicalOptions) is { } parsed)
                {
                    config = parsed;
                    return true;
                }
            }
            catch (JsonException)
            {
                // Malformed JSON falls through to the default below; any other
                // (unexpected) exception is intentionally left to propagate.
            }
        }

        config = new FilterConfig();
        return false;
    }
}
