using BgDataTypes_Lib;

namespace XgFilter_Lib.Filtering;

/// <summary>
/// Optional extension for filters that can decide to skip an entire match or
/// game from header metadata alone, before any rows are yielded. Header-input,
/// pre-stream — distinct from <see cref="IDecisionFilter.ShouldAdvanceGame"/>
/// / <see cref="IDecisionFilter.ShouldAdvanceMatch"/>, which are row-input and
/// evaluated mid-stream on every decision just yielded, whether or not it
/// matched. Implement alongside <see cref="IDecisionFilter"/> where applicable.
///
/// <para>
/// The skip hints carry the same guarantee as the advance hints, from the
/// header rather than a decision: a true vote means no decision in the match
/// or game can pass this filter, so skipping it only saves work (see the hint
/// contract on <see cref="IDecisionFilter"/>). A false vote promises
/// nothing: a filter may skip less than it could, never more.
/// </para>
/// </summary>
internal interface IMatchFilter
{
    /// <summary>
    /// Pre-stream hint: return true only if no decision in
    /// <paramref name="match"/> can pass this filter. Called once per file
    /// after the match header is extracted and before any rows are yielded.
    /// </summary>
    bool ShouldSkipMatch(IMatchInfo match);

    /// <summary>
    /// Pre-stream hint: return true only if no decision in
    /// <paramref name="game"/> can pass this filter. Called once per game
    /// header and before any rows of that game are yielded.
    /// </summary>
    bool ShouldSkipGame(IGameInfo game);
}
