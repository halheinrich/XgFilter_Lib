using ConvertXgToJson_Lib.Models;
using XgFilter_Lib.Classification;
using XgFilter_Lib.Enums;

namespace XgFilter_Lib.Filtering;

/// <summary>
/// Passes rows where the position type (derived from <see cref="DecisionRow.Board"/>)
/// matches any entry in the include list.
/// </summary>
public sealed class PositionTypeFilter : IDecisionFilter
{
    private readonly HashSet<PositionType> _types;

    private static readonly RaceClassifier _race = new();
    private static readonly ContactClassifier _contact = new();

    public PositionTypeFilter(IEnumerable<PositionType> types)
    {
        _types = new HashSet<PositionType>(types);
    }

    public bool Matches(DecisionRow row) =>
        _types.Contains(ClassifyPosition(row.Board));

    private static PositionType ClassifyPosition(int[] board)
    {
        if (_race.Matches(board)) return PositionType.Race;
        if (_contact.Matches(board)) return PositionType.Contact;

        // Fallback — should not be reached since Contact = !Race
        return PositionType.Contact;
    }
}