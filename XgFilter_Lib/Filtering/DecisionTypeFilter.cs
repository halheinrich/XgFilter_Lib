using BgDataTypes_Lib;
using XgFilter_Lib.Enums;

namespace XgFilter_Lib.Filtering;

/// <summary>
/// Passes rows based on whether they are cube decisions or checker plays,
/// as determined by <see cref="DecisionRow.IsCube"/>.
/// </summary>
public sealed class DecisionTypeFilter : IDecisionFilter
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
        DecisionTypeOption.CheckerPlaysOnly => !data.IsCube,
        DecisionTypeOption.CubeOnly         => data.IsCube,
        DecisionTypeOption.Both             =>  true,
        _ => throw new ArgumentOutOfRangeException(
            nameof(_option), _option, "Unknown DecisionTypeOption"),
    };
}
