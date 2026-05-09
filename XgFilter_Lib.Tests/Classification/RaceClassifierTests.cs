using XgFilter_Lib.Classification;
using XgFilter_Lib.Tests.Helpers;

namespace XgFilter_Lib.Tests.Classification;

public class RaceClassifierTests
{
    private static readonly RaceClassifier _sut = new();

    [Fact]
    public void Race_PlayerBearingOff_OpponentBearingOff()
    {
        var board = BoardBuilder.Build((3, 2), (2, 3), (22, -2), (23, -3));
        _sut.Matches(board).Should().BeTrue();
    }

    [Fact]
    public void Race_PlayerAllInHomeboard_OpponentAllInHomeboard()
    {
        var board = BoardBuilder.Build((1, 3), (2, 3), (3, 3), (4, 2), (5, 2), (6, 2),
                              (19, -3), (20, -3), (21, -3), (22, -2), (23, -2), (24, -2));
        _sut.Matches(board).Should().BeTrue();
    }

    [Fact]
    public void Race_PlayerLastOnSix_OpponentFirstOnSeven()
    {
        var board = BoardBuilder.Build((6, 5), (7, -5));
        _sut.Matches(board).Should().BeTrue();
    }

    [Fact]
    public void Race_AdjacentPoints_NoOverlap()
    {
        var board = BoardBuilder.Build((10, 1), (11, -1));
        _sut.Matches(board).Should().BeTrue();
    }

    [Fact]
    public void Contact_StartingPosition()
    {
        var board = BoardBuilder.Build(
            (24, 2), (13, 5), (8, 3), (6, 5),
            (1, -2), (12, -5), (17, -3), (19, -5));
        _sut.Matches(board).Should().BeFalse();
    }

    [Fact]
    public void Contact_PlayerCheckerOnOpponentSide()
    {
        var board = BoardBuilder.Build((20, 1), (6, 14), (19, -15));
        _sut.Matches(board).Should().BeFalse();
    }

    [Fact]
    public void Contact_PlayerOnBar()
    {
        var board = BoardBuilder.Build((25, 1), (6, 14), (19, -15));
        _sut.Matches(board).Should().BeFalse();
    }

    [Fact]
    public void Contact_OpponentOnBar()
    {
        var board = BoardBuilder.Build((6, 15), (0, -1), (19, -14));
        _sut.Matches(board).Should().BeFalse();
    }

    [Fact]
    public void Contact_PlayerAndOpponentIntermixed()
    {
        var board = BoardBuilder.Build((15, 2), (14, -2));
        _sut.Matches(board).Should().BeFalse();
    }

    [Fact]
    public void Contact_PlayerBehindOpponent_ByOnePoint()
    {
        var board = BoardBuilder.Build((8, 1), (7, -1));
        _sut.Matches(board).Should().BeFalse();
    }
}