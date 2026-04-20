using BgDataTypes_Lib;
using XgFilter_Lib.Classification;
using XgFilter_Lib.Enums;

namespace XgFilter_Lib.Filtering;

/// <summary>
/// Passes rows where the board matches any of the selected position types.
/// A position may match multiple types (e.g. Contact AND InnerBoard631).
/// </summary>
public sealed class PositionTypeFilter : IDecisionFilter
{
    private readonly HashSet<PositionType> _types;

    private static readonly RaceClassifier _race = new();
    private static readonly ContactClassifier _contact = new();
    private static readonly InnerBoard631Classifier _innerBoard631 = new();
    private static readonly InnerBoard54321Classifier _innerBoard54321 = new();

    public PositionTypeFilter(IEnumerable<PositionType> types)
    {
        _types = new HashSet<PositionType>(types);
    }

    public bool Matches(IDecisionFilterData data)
    {
        foreach (var type in _types)
            if (Classify(data.Board, type)) return true;
        return false;
    }

    private static bool Classify(IReadOnlyList<int> board, PositionType type) => type switch
    {
        PositionType.Race => _race.Matches(board),
        PositionType.Contact => _contact.Matches(board),
        PositionType.InnerBoard631 => _innerBoard631.Matches(board),
        PositionType.InnerBoard54321 => _innerBoard54321.Matches(board),
        _ => throw new ArgumentOutOfRangeException(
            nameof(type), type, "Unknown PositionType"),
    };
}