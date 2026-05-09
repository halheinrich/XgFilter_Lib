using XgFilter_Lib.Classification;
using XgFilter_Lib.Tests.Helpers;

namespace XgFilter_Lib.Tests.Classification;

public class InnerBoard631ClassifierTests
{
    private static readonly InnerBoard631Classifier _sut = new();

    [Fact]
    public void Matches_PlayerHas631Structure_ReturnsTrue()
    {
        var board = BoardBuilder.Build(
            (6, 2), (3, 2), (1, 2),
            (8, 3), (13, 6),
            (19, -5), (17, -3), (12, -5), (24, -2));
        _sut.Matches(board).Should().BeTrue();
    }

    [Fact]
    public void Matches_PlayerHas631WithExtraCheckers_ReturnsTrue()
    {
        var board = BoardBuilder.Build(
            (6, 3), (3, 3), (1, 3),
            (8, 2), (13, 4),
            (19, -5), (17, -3), (12, -5), (24, -2));
        _sut.Matches(board).Should().BeTrue();
    }

    [Fact]
    public void NoMatch_PositionIsARace_ReturnsFalse()
    {
        var board = BoardBuilder.Build(
            (6, 2), (3, 2), (1, 2),
            (22, -2), (23, -3));
        _sut.Matches(board).Should().BeFalse();
    }

    [Fact]
    public void NoMatch_PlayerMissingOneKey631Point_ReturnsFalse()
    {
        var board = BoardBuilder.Build(
            (6, 2), (3, 1), (1, 2),
            (8, 5), (13, 5),
            (19, -5), (17, -3), (12, -5), (24, -2));
        _sut.Matches(board).Should().BeFalse();
    }

    [Fact]
    public void NoMatch_PlayerHasForbiddenPoint_ReturnsFalse()
    {
        var board = BoardBuilder.Build(
            (6, 2), (5, 2), (3, 2), (1, 2),
            (8, 3), (13, 4),
            (19, -5), (17, -3), (12, -5), (24, -2));
        _sut.Matches(board).Should().BeFalse();
    }

    [Fact]
    public void NoMatch_OnlyOpponentHas631Structure_ReturnsFalse()
    {
        var board = BoardBuilder.Build(
            (13, 5), (10, 5), (7, 5),
            (19, -2), (22, -2), (24, -2),
            (20, -1), (21, -1), (23, -1));
        _sut.Matches(board).Should().BeFalse();
    }
}
