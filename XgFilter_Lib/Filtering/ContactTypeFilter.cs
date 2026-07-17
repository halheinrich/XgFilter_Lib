using BgDataTypes_Lib;
using XgFilter_Lib.Classification;
using XgFilter_Lib.Enums;

namespace XgFilter_Lib.Filtering;

/// <summary>
/// Passes rows whose board matches any of the selected contact types. Because
/// <see cref="ContactType.Contact"/> and <see cref="ContactType.Race"/>
/// partition every position, selecting both is equivalent to no filter and
/// selecting neither (an empty set) admits nothing. Contact-vs-race is a
/// separate axis from the structural patterns in <see cref="PositionTypeFilter"/>,
/// and the two compose via AND across the filter set. Unknown
/// <see cref="ContactType"/> values are rejected at construction rather than on
/// first dispatch.
/// </summary>
internal sealed class ContactTypeFilter : IDecisionFilter
{
    /// <summary>
    /// Single source of truth for the <see cref="ContactType"/> →
    /// <see cref="IPositionClassifier"/> correspondence. Adding a new
    /// contact type means adding one entry here and a matching enum
    /// value in <see cref="ContactType"/>; nothing else inside the
    /// filter needs to change.
    /// </summary>
    private static readonly IReadOnlyDictionary<ContactType, IPositionClassifier> _classifiers =
        new Dictionary<ContactType, IPositionClassifier>
        {
            [ContactType.Contact] = new ContactClassifier(),
            [ContactType.Race]    = new RaceClassifier(),
        };

    private readonly HashSet<ContactType> _types;

    /// <summary>
    /// Creates a filter passing rows that match any of the selected
    /// <paramref name="types"/>. Throws <see cref="ArgumentOutOfRangeException"/>
    /// if <paramref name="types"/> contains an undefined enum value.
    /// </summary>
    public ContactTypeFilter(IEnumerable<ContactType> types)
    {
        _types = new HashSet<ContactType>(types);
        foreach (var type in _types)
            if (!Enum.IsDefined(type))
                throw new ArgumentOutOfRangeException(
                    nameof(types), type, "Unknown ContactType");
    }

    /// <inheritdoc/>
    public bool Matches(IDecisionFilterData data)
    {
        foreach (var type in _types)
            if (_classifiers[type].Matches(data.Board)) return true;
        return false;
    }
}
