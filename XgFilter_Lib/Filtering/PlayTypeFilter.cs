using BgDataTypes_Lib;
using XgFilter_Lib.Classification;
using XgFilter_Lib.Enums;

namespace XgFilter_Lib.Filtering;

/// <summary>
/// Passes checker-play rows whose (priorBoard, afterBestBoard, afterPlayerBoard)
/// triple matches any of the selected play types. Cube decisions always fail —
/// no play was made, so no play-type applies, and the after-boards are empty on
/// cube rows. Checker rows whose after-boards are empty also fail: the producer
/// emits empty <see cref="IDecisionFilterData.AfterBestBoard"/> /
/// <see cref="IDecisionFilterData.AfterPlayerBoard"/> as a sentinel for "no
/// analysed after-state available" (e.g. when the played move is not in XG's
/// candidate set, or no move encoding was emitted for that index), and rows
/// carrying that sentinel are invisible to board-based play-type classification
/// rather than feeding empty arrays into a classifier. OR semantics across
/// selected types; an empty type set yields false (empty OR). Unknown
/// <see cref="PlayType"/> values are rejected at construction rather than on
/// first dispatch.
/// </summary>
internal sealed class PlayTypeFilter : IDecisionFilter
{
    /// <summary>
    /// Single source of truth for the <see cref="PlayType"/> →
    /// <see cref="IPlayTypeClassifier"/> correspondence. Adding a new
    /// play type means adding one entry here and a matching enum value
    /// in <see cref="PlayType"/>; nothing else inside the filter needs
    /// to change.
    /// </summary>
    private static readonly IReadOnlyDictionary<PlayType, IPlayTypeClassifier> _classifiers =
        new Dictionary<PlayType, IPlayTypeClassifier>
        {
            [PlayType.Make20Pt] = new Make20PtClassifier(),
        };

    private readonly HashSet<PlayType> _types;

    /// <summary>
    /// Creates a filter passing rows that match any of the selected
    /// <paramref name="types"/>. Throws <see cref="ArgumentOutOfRangeException"/>
    /// if <paramref name="types"/> contains an undefined enum value.
    /// </summary>
    public PlayTypeFilter(IEnumerable<PlayType> types)
    {
        _types = new HashSet<PlayType>(types);
        foreach (var type in _types)
            if (!Enum.IsDefined(type))
                throw new ArgumentOutOfRangeException(
                    nameof(types), type, "Unknown PlayType");
    }

    /// <summary>
    /// Returns <c>true</c> iff the row is a checker decision with non-empty
    /// after-boards and at least one selected <see cref="PlayType"/>'s
    /// classifier matches the (priorBoard, afterBestBoard, afterPlayerBoard)
    /// triple. Returns <c>false</c> for cube rows (no play was made) and for
    /// checker rows whose after-boards are empty (the producer's sentinel for
    /// "no analysed after-state available" — see the type-level remarks).
    /// </summary>
    public bool Matches(IDecisionFilterData data)
    {
        if (data.IsCube) return false;
        if (data.AfterBestBoard.Count == 0 || data.AfterPlayerBoard.Count == 0)
            return false;

        foreach (var type in _types)
            if (_classifiers[type].Matches(data.Board, data.AfterBestBoard, data.AfterPlayerBoard))
                return true;
        return false;
    }
}
