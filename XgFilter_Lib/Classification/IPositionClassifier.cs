using BgDataTypes_Lib;

namespace XgFilter_Lib.Classification;

/// <summary>
/// Classifies a backgammon position: the board at the moment of the decision,
/// in the frame the producer states on <see cref="IDecisionFilterData.Board"/>
/// (the player on roll's).
/// </summary>
internal interface IPositionClassifier
{
    bool Matches(BoardPosition board);
}
