using BgDataTypes_Lib;
using ConvertXgToJson_Lib;

namespace XgFilter_Lib.Filtering;

/// <summary>
/// Passes rows whose <see cref="IDecisionFilterData.MoveNumber"/> falls within
/// [min, max] (inclusive) AND whose game started from the canonical opening
/// position. Either bound may be omitted (null) to leave that end open.
/// <para>
/// Non-standard-start games (custom problem positions, Bg960, etc.) are
/// dropped wholesale via <see cref="IMatchFilter.ShouldSkipGame"/>; for them
/// no canonical move numbering is meaningful. Within standard-start games,
/// once a decision past <c>max</c> has been seen
/// <see cref="ShouldAdvanceGame"/> signals the iterator to skip the rest of
/// the game — move number is monotonically increasing per game, so no
/// later decision can match.
/// </para>
/// </summary>
public sealed class MoveNumberFilter : IDecisionFilter, IMatchFilter
{
    private readonly int? _min;
    private readonly int? _max;

    public MoveNumberFilter(int? min = null, int? max = null)
    {
        _min = min;
        _max = max;
    }

    public bool Matches(IDecisionFilterData data)
    {
        if (!data.IsStandardStart) return false;
        return (_min is null || data.MoveNumber >= _min.Value) &&
               (_max is null || data.MoveNumber <= _max.Value);
    }

    /// <summary>
    /// Mid-stream early exit: once a decision past <c>max</c> is seen, no
    /// later decision in the same game can match, since move numbers
    /// increase monotonically. Does not need to check
    /// <see cref="IDecisionFilterData.IsStandardStart"/>: non-standard games
    /// are skipped by <see cref="ShouldSkipGame"/> before any rows reach the
    /// row-level pipeline.
    /// </summary>
    public bool ShouldAdvanceGame(IDecisionFilterData data) =>
        _max is int max && data.MoveNumber > max;

    public bool ShouldSkipMatch(XgMatchInfo match) => false;

    public bool ShouldSkipGame(XgGameInfo game) => !game.IsStandardStart;
}
