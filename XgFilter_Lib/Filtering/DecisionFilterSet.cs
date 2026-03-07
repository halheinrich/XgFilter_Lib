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
}
