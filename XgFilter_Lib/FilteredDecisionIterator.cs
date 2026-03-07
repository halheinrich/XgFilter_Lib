using ConvertXgToJson_Lib;
using ConvertXgToJson_Lib.Models;
using XgFilter_Lib.Filtering;

namespace XgFilter_Lib;

/// <summary>
/// Iterates over .xg files in a directory and yields only the
/// <see cref="DecisionRow"/> records that pass the supplied filters.
/// </summary>
public static class FilteredDecisionIterator
{
    /// <summary>
    /// Iterates all .xg files in <paramref name="xgDir"/> and returns
    /// the subset of decisions that match <paramref name="filters"/>.
    /// </summary>
    public static IEnumerable<DecisionRow> IterateXgDirectory(
        string xgDir,
        DecisionFilterSet filters)
    {
        foreach (var row in XgDecisionIterator.IterateXgDirectory(xgDir))
            if (filters.Matches(row))
                yield return row;
    }

    /// <summary>
    /// Iterates all .json files in <paramref name="jsonDir"/> and returns
    /// the subset of decisions that match <paramref name="filters"/>.
    /// </summary>
    public static IEnumerable<DecisionRow> IterateJsonDirectory(
        string jsonDir,
        DecisionFilterSet filters)
    {
        foreach (var row in XgDecisionIterator.IterateJsonDirectory(jsonDir))
            if (filters.Matches(row))
                yield return row;
    }
}