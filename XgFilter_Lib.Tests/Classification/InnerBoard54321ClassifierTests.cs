using XgFilter_Lib.Classification;
using XgFilter_Lib.Tests.Helpers;

namespace XgFilter_Lib.Tests.Classification;

public class InnerBoard54321ClassifierTests
{
    private static readonly InnerBoard54321Classifier _sut = new();

    [Fact]
    public void Matches_PlayerHasFullInnerBoard_Point6Empty_ReturnsTrue()
    {
        var board = BoardBuilder.Build(
            (5, 2), (4, 2), (3, 2), (2, 2), (1, 2),
            (8, 3), (13, 2),
            (19, -5), (17, -3), (12, -5), (24, -2));
        _sut.Matches(board).Should().BeTrue();
    }

    [Fact]
    public void Matches_PlayerHasFullInnerBoard_ExtraCheckersOnKeyPoints_ReturnsTrue()
    {
        var board = BoardBuilder.Build(
            (5, 3), (4, 3), (3, 3), (2, 2), (1, 2),
            (13, 2),
            (19, -5), (17, -3), (12, -5), (24, -2));
        _sut.Matches(board).Should().BeTrue();
    }

    [Fact]
    public void NoMatch_Point6Occupied_ReturnsFalse()
    {
        var board = BoardBuilder.Build(
            (6, 2), (5, 2), (4, 2), (3, 2), (2, 2), (1, 2),
            (13, 3),
            (19, -5), (17, -3), (12, -5), (24, -2));
        _sut.Matches(board).Should().BeFalse();
    }

    [Fact]
    public void NoMatch_MissingOnePoint_ReturnsFalse()
    {
        var board = BoardBuilder.Build(
            (5, 2), (4, 2), (3, 2), (2, 1), (1, 2),
            (8, 4),
            (19, -5), (17, -3), (12, -5), (24, -2));
        _sut.Matches(board).Should().BeFalse();
    }

    [Fact]
    public void NoMatch_RacePosition_ReturnsFalse()
    {
        var board = BoardBuilder.Build(
            (5, 2), (4, 2), (3, 2), (2, 2), (1, 2),
            (22, -2), (23, -3));
        _sut.Matches(board).Should().BeFalse();
    }

    [Fact]
    public void NoMatch_OnlyOpponentHasFullInnerBoard_ReturnsFalse()
    {
        var board = BoardBuilder.Build(
            (13, 5), (10, 5), (7, 5),
            (20, -2), (21, -2), (22, -2), (23, -2), (24, -2),
            (15, -1), (17, -2), (18, -2));
        _sut.Matches(board).Should().BeFalse();
    }
}
