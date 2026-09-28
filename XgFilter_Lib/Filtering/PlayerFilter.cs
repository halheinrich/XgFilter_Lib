using BgDataTypes_Lib;

namespace XgFilter_Lib.Filtering;

/// <summary>
/// Passes decisions whose player — <see cref="IDecisionFilterData.Player"/>,
/// the name of the player on roll — matches any entry in the include list.
/// Comparison is case-insensitive. A decision whose source recorded no name
/// (<see cref="IDecisionFilterData.Player"/> is <see langword="null"/>) never
/// passes: there is no name to match.
/// </summary>
internal sealed class PlayerFilter : IDecisionFilter, IMatchFilter
{
    private readonly HashSet<string> _players;

    /// <summary>
    /// Creates a filter passing rows whose on-roll player name appears in
    /// <paramref name="players"/>. Comparison is case-insensitive.
    /// </summary>
    public PlayerFilter(IEnumerable<string> players)
    {
        _players = new HashSet<string>(players, StringComparer.OrdinalIgnoreCase);
    }

    /// <inheritdoc/>
    public bool Matches(IDecisionFilterData data) =>
        data.Player is { } player && _players.Contains(player);

    /// <summary>
    /// Skip the match if neither player is in the include list.
    /// </summary>
    public bool ShouldSkipMatch(IMatchInfo match) =>
        !_players.Contains(match.Player1) && !_players.Contains(match.Player2);

    /// <summary>
    /// Player names do not change per game — no game-level skip.
    /// </summary>
    public bool ShouldSkipGame(IGameInfo game) => false;
}
