using ConvertXgToJson_Lib.Models;

namespace XgFilter_Lib.Filtering;

/// <summary>
/// A single predicate applied to a <see cref="DecisionRow"/>.
/// </summary>
public interface IDecisionFilter
{
    /// <summary>Returns true if the row satisfies this filter's criteria.</summary>
    bool Matches(DecisionRow row);
}
