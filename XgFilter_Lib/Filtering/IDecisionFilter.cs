using BgDataTypes_Lib;

namespace XgFilter_Lib.Filtering;

public interface IDecisionFilter
{
    bool Matches(IDecisionFilterData data);

    virtual bool ShouldAdvanceGame(IDecisionFilterData data) => false;

    virtual bool ShouldAdvanceMatch(IDecisionFilterData data) => false;
}