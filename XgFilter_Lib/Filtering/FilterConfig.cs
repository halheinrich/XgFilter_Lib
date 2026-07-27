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
/// everything — only when <see cref="AnalysisLevels"/> is empty <em>and</em>
/// both <see cref="IncludeRollouts"/> and <see cref="IncludeBookRollouts"/>
/// are off; see <see cref="Build"/> for the derivation.
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
/// </summary>
public sealed class FilterConfig
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

    /// <summary>Inclusive lower bound on filter-error; null = open lower bound.</summary>
    public double? ErrorMin { get; set; }

    /// <summary>Inclusive upper bound on filter-error; null = open upper bound.</summary>
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
    /// Checked evaluation levels for the depth facet (OR semantics). Empty = no
    /// level constraint (every level admitted, including
    /// <see cref="AnalysisLevel.Unknown"/>). This is raw user intent, one of the
    /// three inputs to the depth facet alongside <see cref="IncludeRollouts"/>
    /// and <see cref="IncludeBookRollouts"/>; <see cref="Build"/> owns the
    /// derivation into a mode set — see there and <see cref="AnalysisDepthFilter"/>.
    /// </summary>
    public IList<AnalysisLevel> AnalysisLevels { get; set; } = new List<AnalysisLevel>();

    /// <summary>
    /// Whether the depth facet admits full rollouts
    /// (<see cref="AnalysisMode.Rollout"/>). One of the two independent mode
    /// toggles; see <see cref="Build"/> for how the toggles and
    /// <see cref="AnalysisLevels"/> compose.
    /// </summary>
    public bool IncludeRollouts { get; set; }

    /// <summary>
    /// Whether the depth facet admits opening-book hits
    /// (<see cref="AnalysisMode.BookRollout"/>). Selecting this with no level
    /// checked is the path by which an unenriched book hit
    /// (<see cref="AnalysisMode.BookRollout"/> + <see cref="AnalysisLevel.Unknown"/>)
    /// is admitted — see <see cref="Build"/>.
    /// </summary>
    public bool IncludeBookRollouts { get; set; }

    /// <summary>
    /// Dice rolls to admit (OR semantics). Empty = no dice filter. Each roll is
    /// canonical and unordered, so listing <c>"31"</c> admits both dice orders;
    /// cube decisions (which carry no roll) never pass an active dice filter.
    /// Serializes as a string-token array (e.g. <c>["31","66"]</c>) via the
    /// converter <see cref="DiceRoll"/> declares on itself, so it needs no
    /// converter registered in <see cref="CanonicalOptions"/> — the same
    /// self-describing case as <see cref="AnalysisLevels"/> and
    /// <see cref="PositionPattern"/>.
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

        // Depth facet (two axes: modes × levels). Inactive iff no level checked
        // AND neither toggle on — the "none selected = pass everything"
        // convention. The predicate is the algebraic collapse of the factory's
        // "any mode toggled or any level checked"; the mode-set derivation
        // itself lives in CreateAnalysisDepthFilter.
        new(FilterFacet.AnalysisDepth,
            static c => c.AnalysisLevels.Count > 0 || c.IncludeRollouts || c.IncludeBookRollouts,
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
    /// turning raw user intent (checked levels plus the two mode toggles) into
    /// the effective mode set. The panel supplies the three inputs verbatim and
    /// must not re-derive the modes. Rollouts toggle → <see cref="AnalysisMode.Rollout"/>,
    /// Book rollouts → <see cref="AnalysisMode.BookRollout"/>; neither toggle →
    /// the <see cref="AnalysisMode.Evaluation"/> default. The level axis
    /// defaults to "any" (an empty <see cref="AnalysisLevels"/>), which
    /// <see cref="AnalysisDepthFilter"/> honours directly. Only called when the
    /// facet is active, so the mode set is never empty.
    /// </summary>
    private AnalysisDepthFilter CreateAnalysisDepthFilter()
    {
        var modes = new List<AnalysisMode>();
        if (IncludeRollouts) modes.Add(AnalysisMode.Rollout);
        if (IncludeBookRollouts) modes.Add(AnalysisMode.BookRollout);
        if (modes.Count == 0) modes.Add(AnalysisMode.Evaluation);
        return new AnalysisDepthFilter(modes, AnalysisLevels);
    }

    /// <summary>
    /// Materializes this configuration as a <see cref="DecisionFilterSet"/>.
    /// Each filter is added only when its corresponding configuration is
    /// non-empty / non-default — the per-facet gates live in the shared
    /// <see cref="FacetRules"/> table, which <see cref="GetActiveFacets"/>
    /// consults too; see the type-level remarks for the empty-list semantics.
    /// </summary>
    /// <exception cref="ArgumentException">
    /// <see cref="MatchScores"/> contains a malformed token — see
    /// <see cref="MatchScoreFilter"/>.
    /// </exception>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <see cref="ContactTypes"/>, <see cref="PositionTypes"/>,
    /// <see cref="PlayTypes"/>, or <see cref="AnalysisLevels"/> contains
    /// an undefined enum value.
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
    /// Consequently this method never throws.
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
    /// <see cref="AnalysisLevels"/> and <see cref="DiceRolls"/> are the same
    /// self-describing case as
    /// <see cref="PositionPattern"/>: <see cref="AnalysisLevel"/> carries its own
    /// type-level <see cref="JsonStringEnumConverter"/> (it is owned by
    /// <c>BgDataTypes_Lib</c>, not <c>XgFilter_Lib.Enums</c>) and
    /// <see cref="DiceRoll"/> carries its own type-level
    /// <c>[JsonConverter]</c> (also owned by <c>BgDataTypes_Lib</c>), so each
    /// serializes as its string form even without the registration above —
    /// <see cref="AnalysisLevel"/> as its declaration name,
    /// <see cref="DiceRoll"/> as its two-digit token (<c>"31"</c>). The shared
    /// <see cref="JsonStringEnumConverter"/> registered here is redundant for
    /// <see cref="AnalysisLevels"/> (and does not touch <see cref="DiceRolls"/>)
    /// but harmless. <see cref="IncludeRollouts"/> /
    /// <see cref="IncludeBookRollouts"/> are plain booleans and need no
    /// converter.
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
