using ConvertXgToJson_Lib;
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

    // -----------------------------------------------------------------------
    //  IMatchFilter: ShouldSkipMatch
    // -----------------------------------------------------------------------

    [Fact]
    public void ShouldSkipMatch_WhenNeitherPlayerInList_ReturnsTrue()
    {
        var filter = new PlayerFilter(["Alice"]);
        var match = new XgMatchInfo { Player1 = "Bob", Player2 = "Charlie" };

        filter.ShouldSkipMatch(match).Should().BeTrue();
    }

    [Fact]
    public void ShouldSkipMatch_WhenPlayer1InList_ReturnsFalse()
    {
        var filter = new PlayerFilter(["Alice"]);
        var match = new XgMatchInfo { Player1 = "Alice", Player2 = "Bob" };

        filter.ShouldSkipMatch(match).Should().BeFalse();
    }

    [Fact]
    public void ShouldSkipMatch_WhenPlayer2InList_ReturnsFalse()
    {
        var filter = new PlayerFilter(["Alice"]);
        var match = new XgMatchInfo { Player1 = "Bob", Player2 = "Alice" };

        filter.ShouldSkipMatch(match).Should().BeFalse();
    }

    [Fact]
    public void ShouldSkipMatch_IsCaseInsensitive()
    {
        var filter = new PlayerFilter(["alice"]);
        var match = new XgMatchInfo { Player1 = "ALICE", Player2 = "Bob" };

        filter.ShouldSkipMatch(match).Should().BeFalse();
    }

    // -----------------------------------------------------------------------
    //  IMatchFilter: ShouldSkipGame
    // -----------------------------------------------------------------------

    [Fact]
    public void ShouldSkipGame_AlwaysReturnsFalse()
    {
        var filter = new PlayerFilter(["Alice"]);
        var game = new XgGameInfo { Away1 = 3, Away2 = 5, IsCrawfordGame = false };

        filter.ShouldSkipGame(game).Should().BeFalse();
    }
}
