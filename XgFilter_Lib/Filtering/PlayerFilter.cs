using BgDataTypes_Lib;
using ConvertXgToJson_Lib;

namespace XgFilter_Lib.Filtering;

/// <summary>
/// Passes rows where <see cref="DecisionRow.Player"/> matches any entry in the include list.
/// Comparison is case-insensitive.
/// </summary>
public sealed class PlayerFilter : IDecisionFilter, IMatchFilter
{
    private readonly HashSet<string> _players;

    public PlayerFilter(IEnumerable<string> players)
    {
        _players = new HashSet<string>(players, StringComparer.OrdinalIgnoreCase);
    }

    public bool Matches(IDecisionFilterData data) => _players.Contains(data.Player);

    /// <summary>
    /// Skip the match if neither player is in the include list.
    /// </summary>
    public bool ShouldSkipMatch(XgMatchInfo match) =>
        !_players.Contains(match.Player1) && !_players.Contains(match.Player2);

    /// <summary>
    /// Player names do not change per game — no game-level skip.
    /// </summary>
    public bool ShouldSkipGame(XgGameInfo game) => false;
}