using ConvertXgToJson_Lib;

namespace XgFilter_Lib.Filtering;

/// <summary>
/// Optional extension for filters that can make skip decisions at the
/// match or game level, before any rows are yielded.
/// Implement alongside <see cref="IDecisionFilter"/> where applicable.
/// </summary>
public interface IMatchFilter
{
    /// <summary>
    /// Returns true if the entire match should be skipped.
    /// Called once per .xg file before any rows are yielded.
    /// </summary>
    bool ShouldSkipMatch(XgMatchInfo match);

    /// <summary>
    /// Returns true if the entire game should be skipped.
    /// Called once per game before any rows are yielded.
    /// </summary>
    bool ShouldSkipGame(XgGameInfo game);
}