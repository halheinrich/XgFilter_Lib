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

    /// <summary>
    /// Adds <paramref name="filter"/> to the set and returns this set for
    /// fluent chaining. Duplicates of the same concrete filter type are
    /// allowed and compose with AND: a row passes only if every entry's
    /// <see cref="IDecisionFilter.Matches"/> returns true. Two
    /// <see cref="ErrorRangeFilter"/> instances, for example, intersect
    /// their ranges; two disjoint <see cref="PlayerFilter"/> instances
    /// will reject every row (the include lists are AND-intersected).
    /// </summary>
    public DecisionFilterSet Add(IDecisionFilter filter)
    {
        _filters.Add(filter);
        return this;
    }

    /// <summary>
    /// True when no filters have been added, in which case <see cref="Matches"/>
    /// admits every row (and the <c>ShouldSkip*</c>/<c>ShouldAdvance*</c> votes
    /// are all false). This is the single source of truth for "no filters
    /// active": consumers must consult it rather than re-inspecting
    /// <see cref="FilterConfig"/> fields, since <see cref="FilterConfig.Build"/>
    /// owns the rule for which fields activate a filter. Mirrors
    /// <see cref="Patterns.BoardPattern.IsEmpty"/>.
    /// </summary>
    public bool IsEmpty => _filters.Count == 0;

    /// <summary>
    /// Returns true if the row passes all filters (or if the set is empty).
    /// </summary>
    public bool Matches(IDecisionFilterData data) => _filters.All(f => f.Matches(data));

    /// <summary>
    /// Pre-stream: true if any <see cref="IMatchFilter"/> in the set votes to
    /// skip the match from header metadata alone.
    /// </summary>
    public bool ShouldSkipMatch(XgMatchInfo match) =>
        _filters.OfType<IMatchFilter>().Any(f => f.ShouldSkipMatch(match));

    /// <summary>
    /// Pre-stream: true if any <see cref="IMatchFilter"/> in the set votes to
    /// skip the game from header metadata alone.
    /// </summary>
    public bool ShouldSkipGame(XgGameInfo game) =>
        _filters.OfType<IMatchFilter>().Any(f => f.ShouldSkipGame(game));

    /// <summary>
    /// Mid-stream: true if any filter — evaluating the just-matched row —
    /// votes to cut the rest of the current game.
    /// </summary>
    public bool ShouldAdvanceGame(IDecisionFilterData data) => _filters.Any(f => f.ShouldAdvanceGame(data));

    /// <summary>
    /// Mid-stream: true if any filter — evaluating the just-matched row —
    /// votes to cut the rest of the current match.
    /// </summary>
    public bool ShouldAdvanceMatch(IDecisionFilterData data) => _filters.Any(f => f.ShouldAdvanceMatch(data));
}
