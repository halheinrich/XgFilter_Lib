using ConvertXgToJson_Lib.Models;

namespace XgFilter_Lib.Filtering;

/// <summary>
/// A single predicate applied to a <see cref="DecisionRow"/>.
/// </summary>
public interface IDecisionFilter
{
    /// <summary>Returns true if the row satisfies this filter's criteria.</summary>
    bool Matches(DecisionRow row);

    /// <summary>
    /// Returns true if remaining decisions in the current game should be
    /// skipped after this row has been yielded. Default: false.
    /// </summary>
    virtual bool ShouldAdvanceGame(DecisionRow row) => false;

    /// <summary>
    /// Returns true if remaining decisions in the current match should be
    /// skipped after this row has been yielded. Default: false.
    /// </summary>
    virtual bool ShouldAdvanceMatch(DecisionRow row) => false;
}