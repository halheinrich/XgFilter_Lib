using XgFilter_Lib.Filtering;
using XgFilter_Lib.Tests.Helpers;

namespace XgFilter_Lib.Tests.Filtering;

public class PlayerFilterTests
{
    [Fact]
    public void Matches_WhenPlayerInList_ReturnsTrue()
    {
        var filter = new PlayerFilter(["Alice", "Bob"]);
        var row = DecisionRowBuilder.Build(player: "Alice");

        filter.Matches(row).Should().BeTrue();
    }

    [Fact]
    public void Matches_WhenPlayerNotInList_ReturnsFalse()
    {
        var filter = new PlayerFilter(["Alice", "Bob"]);
        var row = DecisionRowBuilder.Build(player: "Charlie");

        filter.Matches(row).Should().BeFalse();
    }

    [Fact]
    public void Matches_IsCaseInsensitive()
    {
        var filter = new PlayerFilter(["alice"]);
        var row = DecisionRowBuilder.Build(player: "ALICE");

        filter.Matches(row).Should().BeTrue();
    }

    [Fact]
    public void Matches_WhenListIsEmpty_ReturnsFalse()
    {
        var filter = new PlayerFilter([]);
        var row = DecisionRowBuilder.Build(player: "Alice");

        filter.Matches(row).Should().BeFalse();
    }

    [Fact]
    public void Matches_WhenListHasSingleEntry_MatchesOnlyThatPlayer()
    {
        var filter = new PlayerFilter(["Alice"]);

        filter.Matches(DecisionRowBuilder.Build(player: "Alice")).Should().BeTrue();
        filter.Matches(DecisionRowBuilder.Build(player: "Bob")).Should().BeFalse();
    }
}
