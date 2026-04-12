using BgDataTypes_Lib;

namespace XgFilter_Lib.Filtering;

/// <summary>
/// Passes rows where <see cref="DecisionRow.Error"/> falls within [min, max] (inclusive).
/// Either bound may be omitted (null) to leave that end open.
/// </summary>
public sealed class ErrorRangeFilter : IDecisionFilter
{
    private readonly double? _min;
    private readonly double? _max;

    public ErrorRangeFilter(double? min = null, double? max = null)
    {
        _min = min;
        _max = max;
    }

    public bool Matches(IDecisionFilterData data)
    {
        if (data.FilterError is not double error) return false;
        return (_min is null || error >= _min.Value) &&
               (_max is null || error <= _max.Value);
    }
}
