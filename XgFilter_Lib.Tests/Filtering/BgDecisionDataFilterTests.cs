using BgDataTypes_Lib;
using XgFilter_Lib.Classification;
using XgFilter_Lib.Enums;
using XgFilter_Lib.Filtering;
using XgFilter_Lib.Tests.Helpers;

namespace XgFilter_Lib.Tests.Filtering;

public class BgDecisionDataFilterTests
{
    // -----------------------------------------------------------------------
    //  PlayerFilter
    // -----------------------------------------------------------------------

    [Fact]
    public void PlayerFilter_MatchesPlayer()
    {
        var filter = new PlayerFilter(["Alice"]);
        var data = BgDecisionDataBuilder.Build(player: "Alice");
        filter.Matches(data).Should().BeTrue();
    }

    [Fact]
    public void PlayerFilter_RejectsOtherPlayer()
    {
        var filter = new PlayerFilter(["Alice"]);
        var data = BgDecisionDataBuilder.Build(player: "Bob");
        filter.Matches(data).Should().BeFalse();
    }

    // -----------------------------------------------------------------------
    //  DecisionTypeFilter
    // -----------------------------------------------------------------------

    [Fact]
    public void DecisionTypeFilter_CheckerPlaysOnly_RejectsCube()
    {
        var filter = new DecisionTypeFilter(DecisionTypeOption.CheckerPlaysOnly);
        var data = BgDecisionDataBuilder.Build(isCube: true);
        filter.Matches(data).Should().BeFalse();
    }

    [Fact]
    public void DecisionTypeFilter_CubeOnly_AcceptsCube()
    {
        var filter = new DecisionTypeFilter(DecisionTypeOption.CubeOnly);
        var data = BgDecisionDataBuilder.Build(isCube: true);
        filter.Matches(data).Should().BeTrue();
    }

    // -----------------------------------------------------------------------
    //  ErrorRangeFilter
    // -----------------------------------------------------------------------

    [Fact]
    public void ErrorRangeFilter_WithinRange_ReturnsTrue()
    {
        var filter = new ErrorRangeFilter(min: 0.01, max: 0.10);
        var data = BgDecisionDataBuilder.Build(userPlayError: 0.05);
        filter.Matches(data).Should().BeTrue();
    }

    [Fact]
    public void ErrorRangeFilter_NullError_ReturnsFalse()
    {
        var filter = new ErrorRangeFilter(min: 0.01);
        var data = BgDecisionDataBuilder.Build(userPlayError: null);
        filter.Matches(data).Should().BeFalse();
    }

    [Fact]
    public void ErrorRangeFilter_CubeDecision_UsesDoubleError()
    {
        var filter = new ErrorRangeFilter(min: 0.01, max: 0.10);
        var data = BgDecisionDataBuilder.Build(
            isCube: true, userPlayError: null, userDoubleError: 0.05);
        filter.Matches(data).Should().BeTrue();
    }

    [Fact]
    public void ErrorRangeFilter_CubeDecision_FallsBackToTakeError()
    {
        var filter = new ErrorRangeFilter(min: 0.01, max: 0.10);
        var data = BgDecisionDataBuilder.Build(
            isCube: true, userPlayError: null, userDoubleError: null, userTakeError: 0.03);
        filter.Matches(data).Should().BeTrue();
    }

    // -----------------------------------------------------------------------
    //  MatchScoreFilter
    // -----------------------------------------------------------------------

    [Fact]
    public void MatchScoreFilter_MatchingScore_ReturnsTrue()
    {
        var filter = new MatchScoreFilter(["3a5a"]);
        var data = BgDecisionDataBuilder.Build(onRollNeeds: 3, opponentNeeds: 5);
        filter.Matches(data).Should().BeTrue();
    }

    [Fact]
    public void MatchScoreFilter_Money_ReturnsTrue()
    {
        var filter = new MatchScoreFilter(["money"]);
        var data = BgDecisionDataBuilder.Build(matchLength: 0, onRollNeeds: 0, opponentNeeds: 0);
        filter.Matches(data).Should().BeTrue();
    }

    [Fact]
    public void MatchScoreFilter_Crawford_ReturnsTrue()
    {
        var filter = new MatchScoreFilter(["1a5aC"]);
        var data = BgDecisionDataBuilder.Build(onRollNeeds: 1, opponentNeeds: 5, isCrawford: true);
        filter.Matches(data).Should().BeTrue();
    }

    [Fact]
    public void MatchScoreFilter_CrawfordMismatch_ReturnsFalse()
    {
        var filter = new MatchScoreFilter(["1a5aC"]);
        var data = BgDecisionDataBuilder.Build(onRollNeeds: 1, opponentNeeds: 5, isCrawford: false);
        filter.Matches(data).Should().BeFalse();
    }

    // -----------------------------------------------------------------------
    //  PositionTypeFilter
    // -----------------------------------------------------------------------

    [Fact]
    public void PositionTypeFilter_Race_MatchesRacePosition()
    {
        var filter = new PositionTypeFilter([PositionType.Race]);
        var board = new int[26];
        board[3] = 2; board[2] = 3;
        board[22] = -2; board[23] = -3;
        var data = BgDecisionDataBuilder.Build(board: board);
        filter.Matches(data).Should().BeTrue();
    }

    [Fact]
    public void PositionTypeFilter_Contact_MatchesContactPosition()
    {
        var filter = new PositionTypeFilter([PositionType.Contact]);
        var board = new int[26];
        board[24] = 2; board[13] = 5; board[8] = 3; board[6] = 5;
        board[1] = -2; board[12] = -5; board[17] = -3; board[19] = -5;
        var data = BgDecisionDataBuilder.Build(board: board);
        filter.Matches(data).Should().BeTrue();
    }

    // -----------------------------------------------------------------------
    //  DecisionFilterSet
    // -----------------------------------------------------------------------

    [Fact]
    public void DecisionFilterSet_AllFiltersPass_ReturnsTrue()
    {
        var filters = new DecisionFilterSet()
            .Add(new PlayerFilter(["Alice"]))
            .Add(new DecisionTypeFilter(DecisionTypeOption.CheckerPlaysOnly));
        var data = BgDecisionDataBuilder.Build(player: "Alice", isCube: false);
        filters.Matches(data).Should().BeTrue();
    }

    [Fact]
    public void DecisionFilterSet_OneFilterFails_ReturnsFalse()
    {
        var filters = new DecisionFilterSet()
            .Add(new PlayerFilter(["Alice"]))
            .Add(new DecisionTypeFilter(DecisionTypeOption.CubeOnly));
        var data = BgDecisionDataBuilder.Build(player: "Alice", isCube: false);
        filters.Matches(data).Should().BeFalse();
    }
}