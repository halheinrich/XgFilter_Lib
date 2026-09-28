using BgDataTypes_Lib;
using BgDataTypes_Lib.TestSupport;
using XgFilter_Lib.Filtering;
using XgFilter_Lib.Tests.Helpers;

namespace XgFilter_Lib.Tests.Filtering;

public class PlayerFilterTests
{
    /// <summary>A checker play made by <paramref name="player"/>, the player on roll.</summary>
    private static CheckerPlayDecision PlayedBy(string? player) =>
        TestRecords.CheckerPlay(descriptive: TestRecords.Descriptive(onRollName: player));

    // -----------------------------------------------------------------------
    //  Matches — exercises both substrates of one record
    // -----------------------------------------------------------------------

    [Fact]
    public void Matches_WhenPlayerInList_ReturnsTrue()
    {
        var filter = new PlayerFilter(["Alice", "Bob"]);
        AssertMatchesBoth(filter, PlayedBy("Alice"), expected: true);
    }

    [Fact]
    public void Matches_WhenPlayerNotInList_ReturnsFalse()
    {
        var filter = new PlayerFilter(["Alice", "Bob"]);
        AssertMatchesBoth(filter, PlayedBy("Charlie"), expected: false);
    }

    [Fact]
    public void Matches_IsCaseInsensitive()
    {
        var filter = new PlayerFilter(["alice"]);
        AssertMatchesBoth(filter, PlayedBy("ALICE"), expected: true);
    }

    [Fact]
    public void Matches_WhenListIsEmpty_ReturnsFalse()
    {
        var filter = new PlayerFilter([]);
        AssertMatchesBoth(filter, PlayedBy("Alice"), expected: false);
    }

    [Fact]
    public void Matches_WhenListHasSingleEntry_MatchesOnlyThatPlayer()
    {
        var filter = new PlayerFilter(["Alice"]);
        AssertMatchesBoth(filter, PlayedBy("Alice"), expected: true);
        AssertMatchesBoth(filter, PlayedBy("Bob"), expected: false);
    }

    [Fact]
    public void Matches_NoRecordedName_NeverPasses()
    {
        // A source that recorded no name states none (null, the one spelling
        // of "none recorded"): there is no name to match, so an active player
        // filter drops the decision rather than guessing it into the list.
        var filter = new PlayerFilter(["Alice", "Bob"]);
        AssertMatchesBoth(filter, PlayedBy(null), expected: false);
    }

    // -----------------------------------------------------------------------
    //  IMatchFilter: ShouldSkipMatch — IMatchInfo input, no substrate axis
    // -----------------------------------------------------------------------

    [Fact]
    public void ShouldSkipMatch_WhenNeitherPlayerInList_ReturnsTrue()
    {
        var filter = new PlayerFilter(["Alice"]);
        var match = FakeMatchInfo.Match(7) with { Player1 = "Bob", Player2 = "Charlie" };

        filter.ShouldSkipMatch(match).Should().BeTrue();
    }

    [Fact]
    public void ShouldSkipMatch_WhenPlayer1InList_ReturnsFalse()
    {
        var filter = new PlayerFilter(["Alice"]);
        var match = FakeMatchInfo.Match(7) with { Player1 = "Alice", Player2 = "Bob" };

        filter.ShouldSkipMatch(match).Should().BeFalse();
    }

    [Fact]
    public void ShouldSkipMatch_WhenPlayer2InList_ReturnsFalse()
    {
        var filter = new PlayerFilter(["Alice"]);
        var match = FakeMatchInfo.Match(7) with { Player1 = "Bob", Player2 = "Alice" };

        filter.ShouldSkipMatch(match).Should().BeFalse();
    }

    [Fact]
    public void ShouldSkipMatch_IsCaseInsensitive()
    {
        var filter = new PlayerFilter(["alice"]);
        var match = FakeMatchInfo.Money(isJacoby: true) with { Player1 = "ALICE", Player2 = "Bob" };

        filter.ShouldSkipMatch(match).Should().BeFalse();
    }

    // -----------------------------------------------------------------------
    //  IMatchFilter: ShouldSkipGame
    // -----------------------------------------------------------------------

    [Fact]
    public void ShouldSkipGame_AlwaysReturnsFalse()
    {
        var filter = new PlayerFilter(["Alice"]);

        filter.ShouldSkipGame(FakeGameInfo.Match(3, 5)).Should().BeFalse();
        filter.ShouldSkipGame(FakeGameInfo.Money()).Should().BeFalse();
    }
}
