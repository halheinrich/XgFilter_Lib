namespace XgFilter_Lib.Enums;

/// <summary>
/// Classifies the type of checker play, derived from board state and the candidate moves.
/// </summary>
public enum PlayType
{
    Hit,
    MakePoint,
    Make20Pt,
    HitAndMakePoint,
    SlotAndGo,
    RunningPlay,
}
