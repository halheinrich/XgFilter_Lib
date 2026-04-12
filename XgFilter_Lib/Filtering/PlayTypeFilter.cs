using BgDataTypes_Lib;
using XgFilter_Lib.Enums;

namespace XgFilter_Lib.Filtering;

/// <summary>
/// Passes rows where the play type (derived from board state and candidate moves)
/// matches any entry in the include list.
/// </summary>
/// <remarks>
/// STUB: <see cref="ClassifyPlay"/> always returns <see cref="PlayType.RunningPlay"/>
/// until move-analysis logic is implemented.
/// Only applicable to checker-play rows (<see cref="DecisionRow.IsCube"/> == false).
/// Cube rows always pass.
/// </remarks>
public sealed class PlayTypeFilter : IDecisionFilter
{
    private readonly HashSet<PlayType> _types;

    public PlayTypeFilter(IEnumerable<PlayType> types)
    {
        _types = new HashSet<PlayType>(types);
    }

    public bool Matches(IDecisionFilterData data)
    {
        // Cube decisions have no play type — let them pass.
        if (data.IsCube) return true;
        return _types.Contains(ClassifyPlay(data));
    }

    // -------------------------------------------------------------------
    //  Stub — replace with real move classification logic
    // -------------------------------------------------------------------

    private static PlayType ClassifyPlay(IDecisionFilterData data)
    {
        // TODO: analyse Xgid + candidate moves to determine play type.
        return PlayType.RunningPlay;
    }
}
