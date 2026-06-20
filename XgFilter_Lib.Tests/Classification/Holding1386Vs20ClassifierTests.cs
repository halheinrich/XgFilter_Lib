using XgFilter_Lib.Classification;
using XgFilter_Lib.Tests.Helpers;

namespace XgFilter_Lib.Tests.Classification;

public class Holding1386Vs20ClassifierTests
{
    private static readonly Holding1386Vs20Classifier _sut = new();

    [Fact]
    public void Matches_PlayerHolds1386OpponentAnchorsOn20_ReturnsTrue()
    {
        var board = BoardBuilder.Build(
            (13, 4), (8, 3), (6, 4),
            (24, 2),
            (20, -2), (19, -3), (17, -4));
        _sut.Matches(board).Should().BeTrue();
    }

    // -----------------------------------------------------------------------
    //  Each of the four conditions falsified individually — four negatives
    // -----------------------------------------------------------------------

    [Fact]
    public void NoMatch_PlayerMissing13Point_ReturnsFalse()
    {
        var board = BoardBuilder.Build(
            (13, 1), (8, 3), (6, 4),
            (24, 5),
            (20, -2), (19, -3), (17, -4));
        _sut.Matches(board).Should().BeFalse();
    }

    [Fact]
    public void NoMatch_PlayerMissing8Point_ReturnsFalse()
    {
        var board = BoardBuilder.Build(
            (13, 4), (8, 1), (6, 4),
            (24, 4),
            (20, -2), (19, -3), (17, -4));
        _sut.Matches(board).Should().BeFalse();
    }

    [Fact]
    public void NoMatch_PlayerMissing6Point_ReturnsFalse()
    {
        var board = BoardBuilder.Build(
            (13, 4), (8, 3), (6, 1),
            (24, 5),
            (20, -2), (19, -3), (17, -4));
        _sut.Matches(board).Should().BeFalse();
    }

    [Fact]
    public void NoMatch_OpponentNotAnchoredOn20_ReturnsFalse()
    {
        var board = BoardBuilder.Build(
            (13, 4), (8, 3), (6, 4),
            (24, 2),
            (20, -1), (19, -4), (17, -4));
        _sut.Matches(board).Should().BeFalse();
    }

    // -----------------------------------------------------------------------
    //  Boundaries — each "held" threshold at exactly the cutoff and one short
    // -----------------------------------------------------------------------

    [Fact]
    public void Matches_HeldPointsExactlyTwo_AnchorExactlyTwo_ReturnsTrue()
    {
        // board[6/8/13] == 2 → held; board[20] == -2 → anchored.
        var board = BoardBuilder.Build(
            (13, 2), (8, 2), (6, 2),
            (24, 9),
            (20, -2), (19, -3), (17, -4));
        _sut.Matches(board).Should().BeTrue();
    }

    [Fact]
    public void NoMatch_13PointHasOnlyOne_ReturnsFalse()
    {
        var board = BoardBuilder.Build(
            (13, 1), (8, 2), (6, 2),
            (24, 10),
            (20, -2), (19, -3), (17, -4));
        _sut.Matches(board).Should().BeFalse();
    }

    [Fact]
    public void NoMatch_8PointHasOnlyOne_ReturnsFalse()
    {
        var board = BoardBuilder.Build(
            (13, 2), (8, 1), (6, 2),
            (24, 10),
            (20, -2), (19, -3), (17, -4));
        _sut.Matches(board).Should().BeFalse();
    }

    [Fact]
    public void NoMatch_6PointHasOnlyOne_ReturnsFalse()
    {
        var board = BoardBuilder.Build(
            (13, 2), (8, 2), (6, 1),
            (24, 10),
            (20, -2), (19, -3), (17, -4));
        _sut.Matches(board).Should().BeFalse();
    }

    [Fact]
    public void NoMatch_AnchorHasOnlyOne_ReturnsFalse()
    {
        // board[20] == -1 → opponent has not anchored.
        var board = BoardBuilder.Build(
            (13, 2), (8, 2), (6, 2),
            (24, 9),
            (20, -1), (19, -4), (17, -4));
        _sut.Matches(board).Should().BeFalse();
    }
}
