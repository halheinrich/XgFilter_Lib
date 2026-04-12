using BgDataTypes_Lib;

namespace XgFilter_Lib.Filtering;

/// <summary>
/// Specifies which decision types to include.
/// </summary>
public enum DecisionTypeOption
{
    CheckerPlaysOnly,
    CubeOnly,
    Both,
}

/// <summary>
/// Passes rows based on whether they are cube decisions or checker plays,
/// as determined by <see cref="DecisionRow.IsCube"/>.
/// </summary>
public sealed class DecisionTypeFilter : IDecisionFilter
{
    private readonly DecisionTypeOption _option;

    public DecisionTypeFilter(DecisionTypeOption option)
    {
        _option = option;
    }

    public bool Matches(IDecisionFilterData data) => _option switch
    {
        DecisionTypeOption.CheckerPlaysOnly => !data.IsCube,
        DecisionTypeOption.CubeOnly         => data.IsCube,
        DecisionTypeOption.Both             =>  true,
        _ => true,
    };
}
