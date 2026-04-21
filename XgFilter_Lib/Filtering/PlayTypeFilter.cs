using BgDataTypes_Lib;
using XgFilter_Lib.Classification;

namespace XgFilter_Lib.Filtering;

/// <summary>
/// Passes checker-play rows whose (priorBoard, afterBestBoard, afterPlayerBoard)
/// triple matches any of the supplied <see cref="IPlayTypeClassifier"/>s.
/// Cube decisions always fail — no play was made, so no play-type applies, and
/// the after-boards are empty on cube rows. OR semantics across classifiers;
/// an empty classifier collection yields false (empty OR).
/// </summary>
public sealed class PlayTypeFilter : IDecisionFilter
{
    private readonly IReadOnlyList<IPlayTypeClassifier> _classifiers;

    public PlayTypeFilter(IEnumerable<IPlayTypeClassifier> classifiers)
    {
        _classifiers = classifiers.ToList();
    }

    public bool Matches(IDecisionFilterData data)
    {
        if (data.IsCube) return false;

        foreach (var classifier in _classifiers)
            if (classifier.Matches(data.Board, data.AfterBestBoard, data.AfterPlayerBoard))
                return true;
        return false;
    }
}
