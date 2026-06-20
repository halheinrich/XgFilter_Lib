using BgDataTypes_Lib;
using XgFilter_Lib.Classification;
using XgFilter_Lib.Enums;

namespace XgFilter_Lib.Filtering;

/// <summary>
/// Passes rows where the board matches any of the selected position types.
/// A position may match multiple types (e.g. Contact AND InnerBoard631).
/// Unknown <see cref="PositionType"/> values are rejected at construction
/// rather than on first dispatch.
/// </summary>
public sealed class PositionTypeFilter : IDecisionFilter
{
    /// <summary>
    /// Single source of truth for the <see cref="PositionType"/> →
    /// <see cref="IPositionClassifier"/> correspondence. Adding a new
    /// position type means adding one entry here and a matching enum
    /// value in <see cref="PositionType"/>; nothing else inside the
    /// filter needs to change.
    /// </summary>
    private static readonly IReadOnlyDictionary<PositionType, IPositionClassifier> _classifiers =
        new Dictionary<PositionType, IPositionClassifier>
        {
            [PositionType.Race]            = new RaceClassifier(),
            [PositionType.Contact]         = new ContactClassifier(),
            [PositionType.InnerBoard631]   = new InnerBoard631Classifier(),
            [PositionType.InnerBoard54321] = new InnerBoard54321Classifier(),
            [PositionType.VsTwoPlusUp]     = new VsTwoPlusUpClassifier(),
            [PositionType.Holding1386Vs20] = new Holding1386Vs20Classifier(),
        };

    private readonly HashSet<PositionType> _types;

    /// <summary>
    /// Creates a filter passing rows that match any of the selected
    /// <paramref name="types"/>. Throws <see cref="ArgumentOutOfRangeException"/>
    /// if <paramref name="types"/> contains an undefined enum value.
    /// </summary>
    public PositionTypeFilter(IEnumerable<PositionType> types)
    {
        _types = new HashSet<PositionType>(types);
        foreach (var type in _types)
            if (!Enum.IsDefined(type))
                throw new ArgumentOutOfRangeException(
                    nameof(types), type, "Unknown PositionType");
    }

    /// <inheritdoc/>
    public bool Matches(IDecisionFilterData data)
    {
        foreach (var type in _types)
            if (_classifiers[type].Matches(data.Board)) return true;
        return false;
    }
}
