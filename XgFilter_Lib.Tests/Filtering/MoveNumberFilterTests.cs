using ConvertXgToJson_Lib;
using XgFilter_Lib.Filtering;
using XgFilter_Lib.Tests.Helpers;

namespace XgFilter_Lib.Tests.Filtering;

public class MoveNumberFilterTests
{
    // -----------------------------------------------------------------------
    //  Matches — bounded range, both substrates via RowShape
    // -----------------------------------------------------------------------

    [Fact]
    public void Matches_WhenInRangeAndStandardStart_ReturnsTrue()
    {
        var filter = new MoveNumberFilter(min: 3, max: 10);
        AssertMatchesBoth(
            filter,
            new RowShape(MoveNumber: 5, IsStandardStart: true),
            expected: true);
    }

    [Fact]
    public void Matches_WhenBelowMin_ReturnsFalse()
    {
        var filter = new MoveNumberFilter(min: 3, max: 10);
        AssertMatchesBoth(
            filter,
            new RowShape(MoveNumber: 2, IsStandardStart: true),
            expected: false);
    }

    [Fact]
    public void Matches_WhenAboveMax_ReturnsFalse()
    {
        var filter = new MoveNumberFilter(min: 3, max: 10);
        AssertMatchesBoth(
            filter,
            new RowShape(MoveNumber: 11, IsStandardStart: true),
            expected: false);
    }

    [Fact]
    public void Matches_WhenAtMinBoundary_ReturnsTrue()
    {
        var filter = new MoveNumberFilter(min: 3, max: 10);
        AssertMatchesBoth(
            filter,
            new RowShape(MoveNumber: 3, IsStandardStart: true),
            expected: true);
    }

    [Fact]
    public void Matches_WhenAtMaxBoundary_ReturnsTrue()
    {
        var filter = new MoveNumberFilter(min: 3, max: 10);
        AssertMatchesBoth(
            filter,
            new RowShape(MoveNumber: 10, IsStandardStart: true),
            expected: true);
    }

    [Fact]
    public void Matches_WhenInRangeButNonStandardStart_ReturnsFalse()
    {
        var filter = new MoveNumberFilter(min: 3, max: 10);
        AssertMatchesBoth(
            filter,
            new RowShape(MoveNumber: 5, IsStandardStart: false),
            expected: false);
    }

    [Fact]
    public void Matches_WhenNoBoundsSet_AcceptsAnyStandardStart()
    {
        var filter = new MoveNumberFilter(min: null, max: null);
        AssertMatchesBoth(
            filter,
            new RowShape(MoveNumber: 42, IsStandardStart: true),
            expected: true);
    }

    [Fact]
    public void Matches_WhenNoMinSet_AcceptsLowMoveNumber()
    {
        var filter = new MoveNumberFilter(min: null, max: 10);
        AssertMatchesBoth(
            filter,
            new RowShape(MoveNumber: 1, IsStandardStart: true),
            expected: true);
    }

    [Fact]
    public void Matches_WhenNoMaxSet_AcceptsHighMoveNumber()
    {
        var filter = new MoveNumberFilter(min: 3, max: null);
        AssertMatchesBoth(
            filter,
            new RowShape(MoveNumber: 99, IsStandardStart: true),
            expected: true);
    }

    [Fact]
    public void Matches_WhenNoMinSet_RejectsNonStandardStart()
    {
        var filter = new MoveNumberFilter(min: null, max: 10);
        AssertMatchesBoth(
            filter,
            new RowShape(MoveNumber: 1, IsStandardStart: false),
            expected: false);
    }

    // -----------------------------------------------------------------------
    //  IMatchFilter: ShouldSkipGame — XgGameInfo input, no substrate axis
    // -----------------------------------------------------------------------

    [Fact]
    public void ShouldSkipGame_StandardStart_ReturnsFalse()
    {
        var filter = new MoveNumberFilter(min: 1, max: 5);
        var game = new XgGameInfo { IsStandardStart = true };

        filter.ShouldSkipGame(game).Should().BeFalse();
    }

    [Fact]
    public void ShouldSkipGame_NonStandardStart_ReturnsTrue()
    {
        var filter = new MoveNumberFilter(min: 1, max: 5);
        var game = new XgGameInfo { IsStandardStart = false };

        filter.ShouldSkipGame(game).Should().BeTrue();
    }

    [Fact]
    public void ShouldSkipMatch_AlwaysReturnsFalse()
    {
        var filter = new MoveNumberFilter(min: 1, max: 5);
        var match = new XgMatchInfo { Player1 = "A", Player2 = "B", MatchLength = 7 };

        filter.ShouldSkipMatch(match).Should().BeFalse();
    }

    // -----------------------------------------------------------------------
    //  IDecisionFilter: ShouldAdvanceGame — mid-stream, exercised on both
    // -----------------------------------------------------------------------

    [Fact]
    public void ShouldAdvanceGame_AtMax_ReturnsFalse()
    {
        var filter = new MoveNumberFilter(min: 1, max: 5);
        AssertShouldAdvanceGameBoth(
            filter,
            new RowShape(MoveNumber: 5, IsStandardStart: true),
            expected: false);
    }

    [Fact]
    public void ShouldAdvanceGame_OnePastMax_ReturnsTrue()
    {
        var filter = new MoveNumberFilter(min: 1, max: 5);
        AssertShouldAdvanceGameBoth(
            filter,
            new RowShape(MoveNumber: 6, IsStandardStart: true),
            expected: true);
    }

    [Fact]
    public void ShouldAdvanceGame_NullMax_AlwaysReturnsFalse()
    {
        var filter = new MoveNumberFilter(min: 3, max: null);
        AssertShouldAdvanceGameBoth(
            filter,
            new RowShape(MoveNumber: 9999, IsStandardStart: true),
            expected: false);
    }
}
