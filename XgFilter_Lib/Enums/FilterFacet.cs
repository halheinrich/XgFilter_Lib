using System.ComponentModel;
using XgFilter_Lib.Filtering;

namespace XgFilter_Lib.Enums;

/// <summary>
/// The facet vocabulary of <see cref="FilterConfig"/>: one member per add/skip
/// gate <see cref="FilterConfig.Build"/> recognizes, so a facet is exactly one
/// potential filter in the materialized <see cref="DecisionFilterSet"/>. The
/// analysis-depth inputs (three mode toggles, each qualified by its own level
/// list) are ONE facet — they materialize as a single filter. Members are
/// declared in <see cref="FilterConfig.Build"/>'s add order, which is also the
/// enumeration order of <see cref="FilterConfig.GetActiveFacets"/>. Each member
/// carries a UI-facing label via <see cref="DescriptionAttribute"/> — matching
/// the FilterPanel's visible section headings, so an "N hidden filters active"
/// signal names the sections the user will find on expanding; read it with
/// <see cref="EnumLabel.ToLabel{TEnum}(TEnum)"/>.
/// </summary>
public enum FilterFacet
{
    /// <summary>The player-name include list (<see cref="FilterConfig.Players"/>).</summary>
    [Description("Player names")]
    Players,

    /// <summary>
    /// The checker-play / cube decision-type choice
    /// (<see cref="FilterConfig.DecisionType"/>); the
    /// <see cref="DecisionTypeOption.Both"/> default is the facet's inactive state.
    /// </summary>
    [Description("Decision type")]
    DecisionType,

    /// <summary>The match-score token include list (<see cref="FilterConfig.MatchScores"/>).</summary>
    [Description("Match scores")]
    MatchScores,

    /// <summary>
    /// The filter-error bounds (<see cref="FilterConfig.ErrorMin"/> /
    /// <see cref="FilterConfig.ErrorMax"/>); either bound alone activates the facet.
    /// </summary>
    [Description("Error range")]
    ErrorRange,

    /// <summary>
    /// The move-number bounds (<see cref="FilterConfig.MoveNumberMin"/> /
    /// <see cref="FilterConfig.MoveNumberMax"/>); either bound alone activates the facet.
    /// </summary>
    [Description("Move number range")]
    MoveNumberRange,

    /// <summary>The contact-vs-race include list (<see cref="FilterConfig.ContactTypes"/>).</summary>
    [Description("Contact type")]
    ContactTypes,

    /// <summary>
    /// The structural position-type include list
    /// (<see cref="FilterConfig.PositionTypes"/>). UI-shelved but still
    /// <see cref="FilterConfig.Build"/>-reachable (an old saved config can carry
    /// it), so it stays in the vocabulary; the member retires together with the
    /// facet in the booked removal arc.
    /// </summary>
    [Description("Position types")]
    PositionTypes,

    /// <summary>
    /// The play-type include list (<see cref="FilterConfig.PlayTypes"/>).
    /// UI-shelved but still <see cref="FilterConfig.Build"/>-reachable (an old
    /// saved config can carry it), so it stays in the vocabulary; the member
    /// retires together with the facet in the booked removal arc.
    /// </summary>
    [Description("Play types")]
    PlayTypes,

    /// <summary>
    /// The analysis-depth facet — three per-mode toggles
    /// (<see cref="FilterConfig.IncludeEvaluations"/> /
    /// <see cref="FilterConfig.IncludeRollouts"/> /
    /// <see cref="FilterConfig.IncludeBookRollouts"/>), each qualified by its
    /// own level list. One facet: any toggle activates it (an untoggled level
    /// list is inert), and it materializes as a single filter — a union of
    /// per-mode clauses (see the derivation on <see cref="FilterConfig.Build"/>).
    /// </summary>
    [Description("Analysis depth")]
    AnalysisDepth,

    /// <summary>The dice-roll include list (<see cref="FilterConfig.DiceRolls"/>).</summary>
    [Description("Dice rolls")]
    DiceRolls,

    /// <summary>
    /// The per-location checker-range pattern
    /// (<see cref="FilterConfig.PositionPattern"/>); null and the empty pattern
    /// are both the inactive state.
    /// </summary>
    [Description("Position pattern")]
    PositionPattern,
}
