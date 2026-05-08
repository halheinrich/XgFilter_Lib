using XgFilter_Lib.Enums;

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
/// <see cref="MatchScores"/>, <see cref="PositionTypes"/>, or
/// <see cref="PlayTypes"/> means "no filter of this kind is active" —
/// not "reject everything." <see cref="Build"/> simply skips adding
/// the filter to the set in that case. <see cref="DecisionType"/>
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

    /// <summary>Position types to admit (OR semantics). Empty = no position-type filter.</summary>
    public IList<PositionType> PositionTypes { get; set; } = new List<PositionType>();

    /// <summary>Play types to admit (OR semantics). Empty = no play-type filter.</summary>
    public IList<PlayType> PlayTypes { get; set; } = new List<PlayType>();

    /// <summary>
    /// Materializes this configuration as a <see cref="DecisionFilterSet"/>.
    /// Each filter is added only when its corresponding configuration is
    /// non-empty / non-default; see the type-level remarks for the
    /// empty-list semantics.
    /// </summary>
    /// <exception cref="ArgumentException">
    /// <see cref="MatchScores"/> contains a malformed token — see
    /// <see cref="MatchScoreFilter"/>.
    /// </exception>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <see cref="PositionTypes"/> or <see cref="PlayTypes"/> contains
    /// an undefined enum value.
    /// </exception>
    public DecisionFilterSet Build()
    {
        var set = new DecisionFilterSet();

        if (Players.Count > 0)
            set.Add(new PlayerFilter(Players));

        if (DecisionType != DecisionTypeOption.Both)
            set.Add(new DecisionTypeFilter(DecisionType));

        if (MatchScores.Count > 0)
            set.Add(new MatchScoreFilter(MatchScores));

        if (ErrorMin.HasValue || ErrorMax.HasValue)
            set.Add(new ErrorRangeFilter(ErrorMin, ErrorMax));

        if (MoveNumberMin.HasValue || MoveNumberMax.HasValue)
            set.Add(new MoveNumberFilter(MoveNumberMin, MoveNumberMax));

        if (PositionTypes.Count > 0)
            set.Add(new PositionTypeFilter(PositionTypes));

        if (PlayTypes.Count > 0)
            set.Add(new PlayTypeFilter(PlayTypes));

        return set;
    }
}
