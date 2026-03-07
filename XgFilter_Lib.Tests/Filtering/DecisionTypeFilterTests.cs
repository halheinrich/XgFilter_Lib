using XgFilter_Lib.Filtering;
using XgFilter_Lib.Tests.Helpers;

namespace XgFilter_Lib.Tests.Filtering;

public class DecisionTypeFilterTests
{
    [Fact]
    public void CheckerPlaysOnly_MatchesCheckerPlay()
    {
        var filter = new DecisionTypeFilter(DecisionTypeOption.CheckerPlaysOnly);
        var row = DecisionRowBuilder.Build(roll: 31);

        filter.Matches(row).Should().BeTrue();
    }

    [Fact]
    public void CheckerPlaysOnly_DoesNotMatchCube()
    {
        var filter = new DecisionTypeFilter(DecisionTypeOption.CheckerPlaysOnly);
        var row = DecisionRowBuilder.BuildCube();

        filter.Matches(row).Should().BeFalse();
    }

    [Fact]
    public void CubeOnly_MatchesCube()
    {
        var filter = new DecisionTypeFilter(DecisionTypeOption.CubeOnly);
        var row = DecisionRowBuilder.BuildCube();

        filter.Matches(row).Should().BeTrue();
    }

    [Fact]
    public void CubeOnly_DoesNotMatchCheckerPlay()
    {
        var filter = new DecisionTypeFilter(DecisionTypeOption.CubeOnly);
        var row = DecisionRowBuilder.Build(roll: 31);

        filter.Matches(row).Should().BeFalse();
    }

    [Fact]
    public void Both_MatchesCheckerPlay()
    {
        var filter = new DecisionTypeFilter(DecisionTypeOption.Both);
        var row = DecisionRowBuilder.Build(roll: 31);

        filter.Matches(row).Should().BeTrue();
    }

    [Fact]
    public void Both_MatchesCube()
    {
        var filter = new DecisionTypeFilter(DecisionTypeOption.Both);
        var row = DecisionRowBuilder.BuildCube();

        filter.Matches(row).Should().BeTrue();
    }
}
