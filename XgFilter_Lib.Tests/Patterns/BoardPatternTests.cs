using XgFilter_Lib.Patterns;
using XgFilter_Lib.Tests.Helpers;

namespace XgFilter_Lib.Tests.Patterns;

public class BoardPatternTests
{
    // -----------------------------------------------------------------------
    //  Matches — boundaries, signed constraints, unconstrained indices, empty
    // -----------------------------------------------------------------------

    [Fact]
    public void Matches_InclusiveBoundaries_BothEndsCount()
    {
        var pattern = new BoardPattern([new PointRange(6, 2, 5)]);

        pattern.Matches(BoardBuilder.Build((6, 2))).Should().BeTrue();   // at min
        pattern.Matches(BoardBuilder.Build((6, 5))).Should().BeTrue();   // at max
        pattern.Matches(BoardBuilder.Build((6, 4))).Should().BeTrue();   // interior
        pattern.Matches(BoardBuilder.Build((6, 1))).Should().BeFalse();  // below
        pattern.Matches(BoardBuilder.Build((6, 6))).Should().BeFalse();  // above
    }

    [Fact]
    public void Matches_SignedOpponentConstraint_Honoured()
    {
        // [0,,-2]: opponent has two-or-more on the bar (board[0] <= -2).
        var pattern = new BoardPattern([new PointRange(0, null, -2)]);

        pattern.Matches(BoardBuilder.Build((0, -2))).Should().BeTrue();
        pattern.Matches(BoardBuilder.Build((0, -5))).Should().BeTrue();
        pattern.Matches(BoardBuilder.Build((0, -1))).Should().BeFalse();
        pattern.Matches(BoardBuilder.Build((0, 0))).Should().BeFalse();
    }

    [Fact]
    public void Matches_UnconstrainedIndices_AreIgnored()
    {
        // Only index 6 is constrained; whatever sits elsewhere is irrelevant.
        var pattern = new BoardPattern([new PointRange(6, 2, null)]);

        pattern.Matches(BoardBuilder.Build((6, 2), (8, -7), (25, 3), (0, -4))).Should().BeTrue();
    }

    [Fact]
    public void Matches_AllConstraintsRequired_AndSemantics()
    {
        // Every range must hold — one failing index sinks the whole board.
        var pattern = new BoardPattern([
            new PointRange(6, 2, null),
            new PointRange(8, 2, null),
        ]);

        pattern.Matches(BoardBuilder.Build((6, 3), (8, 3))).Should().BeTrue();
        pattern.Matches(BoardBuilder.Build((6, 3), (8, 1))).Should().BeFalse();
    }

    [Fact]
    public void Matches_EmptyPattern_MatchesEveryBoard()
    {
        BoardPattern.Empty.IsEmpty.Should().BeTrue();
        BoardPattern.Empty.Matches(BoardBuilder.Build()).Should().BeTrue();
        BoardPattern.Empty.Matches(BoardBuilder.Build((0, -5), (6, 9))).Should().BeTrue();
        new BoardPattern([]).Matches(BoardBuilder.Build((1, 3))).Should().BeTrue();
    }

    // -----------------------------------------------------------------------
    //  Construction — duplicate index is the only cross-element invariant
    // -----------------------------------------------------------------------

    [Fact]
    public void Ctor_DuplicateIndex_Throws()
    {
        var act = () => new BoardPattern([new PointRange(6, 2, null), new PointRange(6, null, 5)]);
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Ctor_NullRanges_Throws()
    {
        var act = () => new BoardPattern(null!);
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void Matches_NullBoard_Throws()
    {
        var act = () => BoardPattern.Empty.Matches(null!);
        act.Should().Throw<ArgumentNullException>();
    }

    // -----------------------------------------------------------------------
    //  Parse / ToString round-trip
    // -----------------------------------------------------------------------

    [Fact]
    public void Parse_ValidBracketList_ProducesExpectedRanges()
    {
        var pattern = BoardPattern.Parse("[6,,0] [5,2,] [0,,-1]");

        pattern.Ranges.Should().BeEquivalentTo(new[]
        {
            new PointRange(6, null, 0),
            new PointRange(5, 2, null),
            new PointRange(0, null, -1),
        }, options => options.WithStrictOrdering());
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Parse_BlankInput_ProducesEmptyPattern(string text)
    {
        BoardPattern.Parse(text).IsEmpty.Should().BeTrue();
    }

    [Fact]
    public void Parse_ToleratesIrregularWhitespace()
    {
        var a = BoardPattern.Parse("[6,2,]   [8,2,]");
        var b = BoardPattern.Parse("  [6,2,]\t[8,2,]  ");
        a.ToBracketList().Should().Be(b.ToBracketList());
    }

    [Fact]
    public void ToBracketList_RoundTripsThroughParse()
    {
        const string text = "[0,,-1] [5,2,] [6,,0] [7,0,0] [13,,]";
        var reparsed = BoardPattern.Parse(BoardPattern.Parse(text).ToBracketList());

        reparsed.ToBracketList().Should().Be(text);
    }

    [Fact]
    public void ToString_MatchesToBracketList()
    {
        var pattern = BoardPattern.Parse("[6,2,] [8,2,]");
        pattern.ToString().Should().Be(pattern.ToBracketList());
    }

    [Fact]
    public void ToBracketList_EmptyPattern_IsEmptyString()
    {
        BoardPattern.Empty.ToBracketList().Should().BeEmpty();
    }

    // -----------------------------------------------------------------------
    //  Parse fail-fast cases — each throws; TryParse reports false instead
    // -----------------------------------------------------------------------

    public static TheoryData<string> InvalidBracketLists() => new()
    {
        "[26,,0]",      // index out of range (high)
        "[-1,,0]",      // index out of range (low)
        "[6,5,2]",      // min > max
        "[6,16,]",      // |min| > 15
        "[6,,-16]",     // |max| > 15
        "[6,2,] [6,,5]",// duplicate index
        "6,2,",         // missing brackets
        "[6,2]",        // too few fields
        "[6,2,3,4]",    // too many fields
        "[x,2,3]",      // non-integer index
        "[6,a,]",       // non-integer min
        "[,2,3]",       // empty index field
    };

    [Theory]
    [MemberData(nameof(InvalidBracketLists))]
    public void Parse_InvalidInput_Throws(string text)
    {
        var act = () => BoardPattern.Parse(text);
        act.Should().Throw<Exception>()
            .Which.Should().Match(e => e is FormatException || e is ArgumentException);
    }

    [Theory]
    [MemberData(nameof(InvalidBracketLists))]
    public void TryParse_InvalidInput_ReturnsFalseAndNull(string text)
    {
        BoardPattern.TryParse(text, out var pattern).Should().BeFalse();
        pattern.Should().BeNull();
    }

    [Fact]
    public void TryParse_NullInput_ReturnsFalseAndNull()
    {
        BoardPattern.TryParse(null, out var pattern).Should().BeFalse();
        pattern.Should().BeNull();
    }

    [Fact]
    public void TryParse_ValidInput_ReturnsTrueAndPattern()
    {
        BoardPattern.TryParse("[6,2,] [0,,-2]", out var pattern).Should().BeTrue();
        pattern!.ToBracketList().Should().Be("[6,2,] [0,,-2]");
    }

    [Fact]
    public void Parse_NullInput_Throws()
    {
        var act = () => BoardPattern.Parse(null!);
        act.Should().Throw<ArgumentNullException>();
    }
}
