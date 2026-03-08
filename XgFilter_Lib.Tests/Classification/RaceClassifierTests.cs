using XgFilter_Lib.Classification;

namespace XgFilter_Lib.Tests.Classification;

public class RaceClassifierTests
{
    private static readonly RaceClassifier _sut = new();

    private static int[] MakeBoard(params (int index, int count)[] points)
    {
        var board = new int[26];
        foreach (var (index, count) in points)
            board[index] = count;
        return board;
    }

    [Fact]
    public void Race_PlayerBearingOff_OpponentBearingOff()
    {
        var board = MakeBoard((3, 2), (2, 3), (22, -2), (23, -3));
        _sut.Matches(board).Should().BeTrue();
    }

    [Fact]
    public void Race_PlayerAllInHomeboard_OpponentAllInHomeboard()
    {
        var board = MakeBoard((1, 3), (2, 3), (3, 3), (4, 2), (5, 2), (6, 2),
                              (19, -3), (20, -3), (21, -3), (22, -2), (23, -2), (24, -2));
        _sut.Matches(board).Should().BeTrue();
    }

    [Fact]
    public void Race_PlayerLastOnSix_OpponentFirstOnSeven()
    {
        var board = MakeBoard((6, 5), (7, -5));
        _sut.Matches(board).Should().BeTrue();
    }

    [Fact]
    public void Race_AdjacentPoints_NoOverlap()
    {
        var board = MakeBoard((10, 1), (11, -1));
        _sut.Matches(board).Should().BeTrue();
    }

    [Fact]
    public void Contact_StartingPosition()
    {
        var board = MakeBoard(
            (24, 2), (13, 5), (8, 3), (6, 5),
            (1, -2), (12, -5), (17, -3), (19, -5));
        _sut.Matches(board).Should().BeFalse();
    }

    [Fact]
    public void Contact_PlayerCheckerOnOpponentSide()
    {
        var board = MakeBoard((20, 1), (6, 14), (19, -15));
        _sut.Matches(board).Should().BeFalse();
    }

    [Fact]
    public void Contact_PlayerOnBar()
    {
        var board = MakeBoard((25, 1), (6, 14), (19, -15));
        _sut.Matches(board).Should().BeFalse();
    }

    [Fact]
    public void Contact_OpponentOnBar()
    {
        var board = MakeBoard((6, 15), (0, -1), (19, -14));
        _sut.Matches(board).Should().BeFalse();
    }

    [Fact]
    public void Contact_PlayerAndOpponentIntermixed()
    {
        var board = MakeBoard((15, 2), (14, -2));
        _sut.Matches(board).Should().BeFalse();
    }

    [Fact]
    public void Contact_PlayerBehindOpponent_ByOnePoint()
    {
        var board = MakeBoard((8, 1), (7, -1));
        _sut.Matches(board).Should().BeFalse();
    }
}

public class ContactClassifierTests
{
    private static readonly ContactClassifier _sut = new();

    [Fact]
    public void Contact_StartingPosition_ReturnsTrue()
    {
        var board = new int[26];
        board[24] = 2; board[13] = 5; board[8] = 3; board[6] = 5;
        board[1] = -2; board[12] = -5; board[17] = -3; board[19] = -5;
        _sut.Matches(board).Should().BeTrue();
    }

    [Fact]
    public void Contact_BearingOffPosition_ReturnsFalse()
    {
        var board = new int[26];
        board[3] = 2; board[2] = 3;
        board[22] = -2; board[23] = -3;
        _sut.Matches(board).Should().BeFalse();
    }
}