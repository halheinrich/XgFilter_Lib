using BgDataTypes_Lib;
using XgFilter_Lib.Enums;

namespace XgFilter_Lib.Filtering;

/// <summary>
/// Passes decisions by kind — checker plays, cube decisions, or both — as
/// <see cref="IDecisionFilterData.Kind"/> states it.
/// </summary>
internal sealed class DecisionTypeFilter : IDecisionFilter
{
    private readonly DecisionTypeOption _option;

    /// <summary>Creates a filter that admits decisions of the given <paramref name="option"/>.</summary>
    public DecisionTypeFilter(DecisionTypeOption option)
    {
        _option = option;
    }

    /// <inheritdoc/>
    public bool Matches(IDecisionFilterData data) => _option switch
    {
        DecisionTypeOption.CheckerPlaysOnly => data.Kind == DecisionKind.CheckerPlay,
        DecisionTypeOption.CubeOnly         => data.Kind == DecisionKind.Cube,
        DecisionTypeOption.Both             => true,
        _ => throw new ArgumentOutOfRangeException(
            nameof(_option), _option, "Unknown DecisionTypeOption"),
    };
}
