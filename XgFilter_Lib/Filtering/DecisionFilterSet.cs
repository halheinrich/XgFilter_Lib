using ConvertXgToJson_Lib;
using ConvertXgToJson_Lib.Models;

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
    public bool Matches(DecisionRow row) =>
        _filters.All(f => f.Matches(row));

    /// <summary>
    /// Applies this filter set to a sequence of rows, returning only those that pass.
    /// </summary>
    public IEnumerable<DecisionRow> Apply(IEnumerable<DecisionRow> rows) =>
        rows.Where(Matches);

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
    public bool ShouldAdvanceGame(DecisionRow row) =>
        _filters.Any(f => f.ShouldAdvanceGame(row));

    /// <summary>
    /// Returns true if any filter votes to skip remaining decisions in this match.
    /// </summary>
    public bool ShouldAdvanceMatch(DecisionRow row) =>
        _filters.Any(f => f.ShouldAdvanceMatch(row));
}