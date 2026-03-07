using ConvertXgToJson_Lib.Models;

namespace XgFilter_Lib.Filtering;

/// <summary>
/// Passes rows where <see cref="DecisionRow.MatchScore"/> matches any entry in the include list.
/// Examples: "5a5a", "3a1aC", "DMP", "money".
/// Comparison is case-insensitive.
/// </summary>
public sealed class MatchScoreFilter : IDecisionFilter
{
    private readonly HashSet<string> _scores;

    public MatchScoreFilter(IEnumerable<string> scores)
    {
        _scores = new HashSet<string>(scores, StringComparer.OrdinalIgnoreCase);
    }

    public bool Matches(DecisionRow row) =>
        _scores.Contains(row.MatchScore);
}
