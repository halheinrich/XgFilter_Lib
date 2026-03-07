using XgFilter_Lib.Filtering;
using XgFilter_Lib.Tests.Helpers;

namespace XgFilter_Lib.Tests.Filtering;

public class MatchScoreFilterTests
{
    [Fact]
    public void Matches_WhenScoreInList_ReturnsTrue()
    {
        var filter = new MatchScoreFilter(["3a5aC", "money"]);
        var row = DecisionRowBuilder.Build(matchScore: "3a5aC");

        filter.Matches(row).Should().BeTrue();
    }

    [Fact]
    public void Matches_WhenScoreNotInList_ReturnsFalse()
    {
        var filter = new MatchScoreFilter(["3a5aC"]);
        var row = DecisionRowBuilder.Build(matchScore: "2a4aC");

        filter.Matches(row).Should().BeFalse();
    }

    [Fact]
    public void Matches_IsCaseInsensitive()
    {
        var filter = new MatchScoreFilter(["money"]);
        var row = DecisionRowBuilder.Build(matchScore: "MONEY");

        filter.Matches(row).Should().BeTrue();
    }

    [Fact]
    public void Matches_WhenListIsEmpty_ReturnsFalse()
    {
        var filter = new MatchScoreFilter([]);
        var row = DecisionRowBuilder.Build(matchScore: "3a5aC");

        filter.Matches(row).Should().BeFalse();
    }

    [Fact]
    public void Matches_MoneyGame()
    {
        var filter = new MatchScoreFilter(["money"]);
        var row = DecisionRowBuilder.Build(matchScore: "money");

        filter.Matches(row).Should().BeTrue();
    }
}
