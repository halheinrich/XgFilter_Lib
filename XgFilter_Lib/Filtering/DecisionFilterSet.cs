using BgDataTypes_Lib;
using ConvertXgToJson_Lib;

namespace XgFilter_Lib.Filtering;

/// <summary>
/// An ordered list of <see cref="IDecisionFilter"/> instances combined with AND semantics.
/// A row must satisfy every filter to pass.
/// </summary>
public sealed class DecisionFilterSet
{
    private readonly List<IDecisionFilter> _filters = [];

    /// <summary>Adds a filter to the set.</summary>
    public DecisionFilterSet Add(IDecisionFilter filter)
    {
        _filters.Add(filter);
        return this;
    }

    /// <summary>
    /// Returns true if the row passes all filters (or if the set is empty).
    /// </summary>
    public bool Matches(IDecisionFilterData data) => _filters.All(f => f.Matches(data));

    /// <summary>
    /// Returns true if any IMatchFilter in the set votes to skip this match.
    /// </summary>
    public bool ShouldSkipMatch(XgMatchInfo match) =>
        _filters.OfType<IMatchFilter>().Any(f => f.ShouldSkipMatch(match));

    /// <summary>
    /// Returns true if any IMatchFilter in the set votes to skip this game.
    /// </summary>
    public bool ShouldSkipGame(XgGameInfo game) =>
        _filters.OfType<IMatchFilter>().Any(f => f.ShouldSkipGame(game));

    /// <summary>
    /// Returns true if any filter votes to skip remaining decisions in this game.
    /// </summary>
    public bool ShouldAdvanceGame(IDecisionFilterData data) => _filters.Any(f => f.ShouldAdvanceGame(data));

    /// <summary>
    /// Returns true if any filter votes to skip remaining decisions in this match.
    /// </summary>
    public bool ShouldAdvanceMatch(IDecisionFilterData data) => _filters.Any(f => f.ShouldAdvanceMatch(data));
}