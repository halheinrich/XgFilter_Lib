using XgFilter_Lib.Enums;
using XgFilter_Lib.Filtering;
using XgFilter_Lib.Tests.Helpers;
using static XgFilter_Lib.Tests.Helpers.DecisionFilterAsserts;

namespace XgFilter_Lib.Tests.Filtering;

public class DecisionTypeFilterTests
{
    [Fact]
    public void CheckerPlaysOnly_MatchesCheckerPlay()
    {
        var filter = new DecisionTypeFilter(DecisionTypeOption.CheckerPlaysOnly);
        AssertMatchesBoth(filter, new RowShape(IsCube: false), expected: true);
    }

    [Fact]
    public void CheckerPlaysOnly_DoesNotMatchCube()
    {
        var filter = new DecisionTypeFilter(DecisionTypeOption.CheckerPlaysOnly);
        AssertMatchesBoth(filter, new RowShape(IsCube: true), expected: false);
    }

    [Fact]
    public void CubeOnly_MatchesCube()
    {
        var filter = new DecisionTypeFilter(DecisionTypeOption.CubeOnly);
        AssertMatchesBoth(filter, new RowShape(IsCube: true), expected: true);
    }

    [Fact]
    public void CubeOnly_DoesNotMatchCheckerPlay()
    {
        var filter = new DecisionTypeFilter(DecisionTypeOption.CubeOnly);
        AssertMatchesBoth(filter, new RowShape(IsCube: false), expected: false);
    }

    [Fact]
    public void Both_MatchesCheckerPlay()
    {
        var filter = new DecisionTypeFilter(DecisionTypeOption.Both);
        AssertMatchesBoth(filter, new RowShape(IsCube: false), expected: true);
    }

    [Fact]
    public void Both_MatchesCube()
    {
        var filter = new DecisionTypeFilter(DecisionTypeOption.Both);
        AssertMatchesBoth(filter, new RowShape(IsCube: true), expected: true);
    }
}
