using ConvertXgToJson_Lib.Models;
using XgFilter_Lib.Enums;

namespace XgFilter_Lib.Filtering;

/// <summary>
/// Passes rows where the position type (derived from <see cref="DecisionRow.Xgid"/>)
/// matches any entry in the include list.
/// </summary>
/// <remarks>
/// STUB: <see cref="ClassifyPosition"/> always returns <see cref="PositionType.Contact"/>
/// until the XGID decoder is implemented.
/// </remarks>
public sealed class PositionTypeFilter : IDecisionFilter
{
    private readonly HashSet<PositionType> _types;

    public PositionTypeFilter(IEnumerable<PositionType> types)
    {
        _types = new HashSet<PositionType>(types);
    }

    public bool Matches(DecisionRow row) =>
        _types.Contains(ClassifyPosition(row.Xgid));

    // -------------------------------------------------------------------
    //  Stub — replace with real XGID board-state analysis
    // -------------------------------------------------------------------

    private static PositionType ClassifyPosition(string xgid)
    {
        // TODO: decode checker positions from xgid and classify.
        return PositionType.Contact;
    }
}
