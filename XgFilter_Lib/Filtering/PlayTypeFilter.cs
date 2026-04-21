using BgDataTypes_Lib;
using XgFilter_Lib.Classification;
using XgFilter_Lib.Enums;

namespace XgFilter_Lib.Filtering;

/// <summary>
/// Passes checker-play rows whose (priorBoard, afterBestBoard, afterPlayerBoard)
/// triple matches any of the selected play types. Cube decisions always fail —
/// no play was made, so no play-type applies, and the after-boards are empty on
/// cube rows. OR semantics across selected types; an empty type set yields false
/// (empty OR).
/// </summary>
public sealed class PlayTypeFilter : IDecisionFilter
{
    private readonly HashSet<PlayType> _types;

    private static readonly Make20PtClassifier _make20Pt = new();

    public PlayTypeFilter(IEnumerable<PlayType> types)
    {
        _types = new HashSet<PlayType>(types);
    }

    public bool Matches(IDecisionFilterData data)
    {
        if (data.IsCube) return false;

        foreach (var type in _types)
            if (Classify(data.Board, data.AfterBestBoard, data.AfterPlayerBoard, type))
                return true;
        return false;
    }

    private static bool Classify(
        IReadOnlyList<int> priorBoard,
        IReadOnlyList<int> afterBestBoard,
        IReadOnlyList<int> afterPlayerBoard,
        PlayType type) => type switch
    {
        PlayType.Make20Pt => _make20Pt.Matches(priorBoard, afterBestBoard, afterPlayerBoard),
        _ => throw new ArgumentOutOfRangeException(
            nameof(type), type, "Unknown PlayType"),
    };
}
