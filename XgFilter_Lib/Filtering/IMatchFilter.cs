using ConvertXgToJson_Lib;

namespace XgFilter_Lib.Filtering;

/// <summary>
/// Optional extension for filters that can decide to skip an entire match or
/// game from header metadata alone, before any rows are yielded. Header-input,
/// pre-stream — distinct from <see cref="IDecisionFilter.ShouldAdvanceGame"/>
/// / <see cref="IDecisionFilter.ShouldAdvanceMatch"/>, which are row-input and
/// evaluated mid-stream after a row has matched. Implement alongside
/// <see cref="IDecisionFilter"/> where applicable.
/// </summary>
public interface IMatchFilter
{
    /// <summary>
    /// Pre-stream hint: return true if no row in <paramref name="match"/> can
    /// match this filter. Called once per .xg file after the match header is
    /// extracted and before any rows are yielded.
    /// </summary>
    bool ShouldSkipMatch(XgMatchInfo match);

    /// <summary>
    /// Pre-stream hint: return true if no row in <paramref name="game"/> can
    /// match this filter. Called once per game header and before any rows of
    /// that game are yielded.
    /// </summary>
    bool ShouldSkipGame(XgGameInfo game);
}
