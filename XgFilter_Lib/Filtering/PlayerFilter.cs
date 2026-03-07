using ConvertXgToJson_Lib.Models;

namespace XgFilter_Lib.Filtering;

/// <summary>
/// Passes rows where <see cref="DecisionRow.Player"/> matches any entry in the include list.
/// Comparison is case-insensitive.
/// </summary>
public sealed class PlayerFilter : IDecisionFilter
{
    private readonly HashSet<string> _players;

    public PlayerFilter(IEnumerable<string> players)
    {
        _players = new HashSet<string>(players, StringComparer.OrdinalIgnoreCase);
    }

    public bool Matches(DecisionRow row) =>
        _players.Contains(row.Player);
}
