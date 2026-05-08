using BgDataTypes_Lib;
using XgFilter_Lib.Classification;
using XgFilter_Lib.Enums;

namespace XgFilter_Lib.Filtering;

/// <summary>
/// Passes checker-play rows whose (priorBoard, afterBestBoard, afterPlayerBoard)
/// triple matches any of the selected play types. Cube decisions always fail —
/// no play was made, so no play-type applies, and the after-boards are empty on
/// cube rows. OR semantics across selected types; an empty type set yields false
/// (empty OR). Unknown <see cref="PlayType"/> values are rejected at
/// construction rather than on first dispatch.
/// </summary>
public sealed class PlayTypeFilter : IDecisionFilter
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

    /// <inheritdoc/>
    public bool Matches(IDecisionFilterData data)
    {
        if (data.IsCube) return false;

        foreach (var type in _types)
            if (_classifiers[type].Matches(data.Board, data.AfterBestBoard, data.AfterPlayerBoard))
                return true;
        return false;
    }
}
