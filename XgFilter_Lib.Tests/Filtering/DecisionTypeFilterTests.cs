using BgDataTypes_Lib.TestSupport;
using XgFilter_Lib.Enums;
using XgFilter_Lib.Filtering;

namespace XgFilter_Lib.Tests.Filtering;

public class DecisionTypeFilterTests
{
    [Fact]
    public void CheckerPlaysOnly_MatchesCheckerPlay()
    {
        var filter = new DecisionTypeFilter(DecisionTypeOption.CheckerPlaysOnly);
        AssertMatchesBoth(filter, TestRecords.CheckerPlay(), expected: true);
    }

    [Fact]
    public void CheckerPlaysOnly_DoesNotMatchCube()
    {
        var filter = new DecisionTypeFilter(DecisionTypeOption.CheckerPlaysOnly);
        AssertMatchesBoth(filter, TestRecords.Cube(), expected: false);
    }

    [Fact]
    public void CubeOnly_MatchesCube()
    {
        var filter = new DecisionTypeFilter(DecisionTypeOption.CubeOnly);
        AssertMatchesBoth(filter, TestRecords.Cube(), expected: true);
    }

    [Fact]
    public void CubeOnly_DoesNotMatchCheckerPlay()
    {
        var filter = new DecisionTypeFilter(DecisionTypeOption.CubeOnly);
        AssertMatchesBoth(filter, TestRecords.CheckerPlay(), expected: false);
    }

    [Fact]
    public void Both_MatchesCheckerPlay()
    {
        var filter = new DecisionTypeFilter(DecisionTypeOption.Both);
        AssertMatchesBoth(filter, TestRecords.CheckerPlay(), expected: true);
    }

    [Fact]
    public void Both_MatchesCube()
    {
        var filter = new DecisionTypeFilter(DecisionTypeOption.Both);
        AssertMatchesBoth(filter, TestRecords.Cube(), expected: true);
    }
}
