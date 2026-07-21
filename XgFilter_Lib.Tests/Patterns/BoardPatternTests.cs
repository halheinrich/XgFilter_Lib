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
        var pattern = new BoardPattern([new CheckerRange(6, 2, 5)]);

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
        var pattern = new BoardPattern([new CheckerRange(0, null, -2)]);

        pattern.Matches(BoardBuilder.Build((0, -2))).Should().BeTrue();
        pattern.Matches(BoardBuilder.Build((0, -5))).Should().BeTrue();
        pattern.Matches(BoardBuilder.Build((0, -1))).Should().BeFalse();
        pattern.Matches(BoardBuilder.Build((0, 0))).Should().BeFalse();
    }

    [Fact]
    public void Matches_UnconstrainedIndices_AreIgnored()
    {
        // Only index 6 is constrained; whatever sits elsewhere is irrelevant.
        var pattern = new BoardPattern([new CheckerRange(6, 2, null)]);

        pattern.Matches(BoardBuilder.Build((6, 2), (8, -7), (25, 3), (0, -4))).Should().BeTrue();
    }

    [Fact]
    public void Matches_AllConstraintsRequired_AndSemantics()
    {
        // Every range must hold — one failing index sinks the whole board.
        var pattern = new BoardPattern([
            new CheckerRange(6, 2, null),
            new CheckerRange(8, 2, null),
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
    //  Matches — borne-off locations, derived from the board (15 minus on-board
    //  sum, bars included; opponent's value signed negative)
    // -----------------------------------------------------------------------

    // The standard starting position: fifteen checkers per side on the board,
    // so nobody is off.
    private static int[] FullBoard() => BoardBuilder.Build(
        (24, 2), (13, 5), (8, 3), (6, 5),
        (1, -2), (12, -5), (17, -3), (19, -5));

    [Fact]
    public void Matches_OffLocations_NobodyOff_ZeroCountsMatch()
    {
        var board = FullBoard();

        BoardPattern.Parse("[off,0,0] [opp-off,0,0]").Matches(board).Should().BeTrue();
        BoardPattern.Parse("[off,1,]").Matches(board).Should().BeFalse();
        BoardPattern.Parse("[opp-off,,-1]").Matches(board).Should().BeFalse();
    }

    [Fact]
    public void Matches_OffLocations_AllFifteenOff_BoundsAtTheCeilingMatch()
    {
        // An empty board: both sides have borne off all fifteen.
        var board = BoardBuilder.Build();

        BoardPattern.Parse("[off,15,15] [opp-off,-15,-15]").Matches(board).Should().BeTrue();
        BoardPattern.Parse("[off,,14]").Matches(board).Should().BeFalse();
        BoardPattern.Parse("[opp-off,-14,]").Matches(board).Should().BeFalse();
    }

    [Fact]
    public void Matches_PlayerOff_DerivedFromOnBoardSum()
    {
        // Player has 10 on the board → 5 off.
        var board = BoardBuilder.Build((6, 4), (13, 6), (12, -15));

        BoardPattern.Parse("[off,5,5]").Matches(board).Should().BeTrue();
        BoardPattern.Parse("[off,,4]").Matches(board).Should().BeFalse();
        BoardPattern.Parse("[off,6,]").Matches(board).Should().BeFalse();
    }

    [Fact]
    public void Matches_OpponentOff_SignedNegative_MoreOffIsMoreNegative()
    {
        // Opponent has 13 on the board → 2 off → derived value -2.
        var twoOff = BoardBuilder.Build((6, 15), (19, -8), (12, -5));
        // Opponent has 14 on the board → 1 off → derived value -1.
        var oneOff = BoardBuilder.Build((6, 15), (19, -9), (12, -5));

        // "[opp-off,,-2]": opponent has two or more off — the B1 signed
        // convention, reading exactly like "[5,,-2]".
        BoardPattern.Parse("[opp-off,,-2]").Matches(twoOff).Should().BeTrue();
        BoardPattern.Parse("[opp-off,,-2]").Matches(oneOff).Should().BeFalse();
        BoardPattern.Parse("[opp-off,-2,]").Matches(oneOff).Should().BeTrue();
    }

    [Fact]
    public void Matches_OffLocations_BarCheckersCountAsOnBoard()
    {
        // Player: 12 on points + 3 on the bar (index 25) → 0 off.
        // Opponent: all 15 on their bar (index 0) → 0 off.
        var board = BoardBuilder.Build((6, 6), (13, 6), (25, 3), (0, -15));

        BoardPattern.Parse("[off,0,0] [opp-off,0,0]").Matches(board).Should().BeTrue();
    }

    [Fact]
    public void Matches_OffLocationCombinedWithPointConstraint_BothMustHold()
    {
        var pattern = BoardPattern.Parse("[6,2,] [off,1,]");

        // 6-point made and 9 off — both constraints hold.
        pattern.Matches(BoardBuilder.Build((6, 2), (13, 4))).Should().BeTrue();
        // 6-point made but all fifteen on the board — the off constraint sinks it.
        pattern.Matches(BoardBuilder.Build((6, 2), (13, 13))).Should().BeFalse();
        // 4 off but the 6-point not made — the point constraint sinks it.
        pattern.Matches(BoardBuilder.Build((6, 1), (13, 10))).Should().BeFalse();
    }

    // -----------------------------------------------------------------------
    //  Construction — duplicate location is the only cross-element invariant
    // -----------------------------------------------------------------------

    [Fact]
    public void Ctor_DuplicateIndex_Throws()
    {
        var act = () => new BoardPattern([new CheckerRange(6, 2, null), new CheckerRange(6, null, 5)]);
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Ctor_DuplicateOffLocation_Throws()
    {
        var act = () => new BoardPattern([
            new CheckerRange(CheckerLocation.PlayerOff, 1, null),
            new CheckerRange(CheckerLocation.PlayerOff, null, 5),
        ]);
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
            new CheckerRange(6, null, 0),
            new CheckerRange(5, 2, null),
            new CheckerRange(0, null, -1),
        }, options => options.WithStrictOrdering());
    }

    [Fact]
    public void Parse_NamedOffTokens_ProduceOffCheckerRanges()
    {
        var pattern = BoardPattern.Parse("[off,1,] [opp-off,,-2]");

        pattern.Ranges.Should().BeEquivalentTo(new[]
        {
            new CheckerRange(CheckerLocation.PlayerOff, 1, null),
            new CheckerRange(CheckerLocation.OpponentOff, null, -2),
        }, options => options.WithStrictOrdering());
    }

    [Fact]
    public void Parse_NamedTokens_AreCaseInsensitive_RenderCanonicalLowerCase()
    {
        BoardPattern.Parse("[OFF,1,] [Opp-Off,,-2]")
            .ToBracketList().Should().Be("[off,1,] [opp-off,,-2]");
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
    public void ToBracketList_OffTokens_RoundTripThroughParse()
    {
        const string text = "[0,,-1] [6,2,] [off,1,15] [opp-off,-15,-2]";
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
        "[x,2,3]",      // unrecognized location head
        "[6,a,]",       // non-integer min
        "[,2,3]",       // empty location field
        "[offf,1,]",    // unknown location name
        "[off,-1,]",    // wrong-signed bound: player's off count is never negative
        "[opp-off,,1]", // wrong-signed bound: opponent's off count is never positive
        "[off,,16]",    // off bound beyond the 15-checker ceiling
        "[opp-off,-16,]", // opp-off bound beyond the ceiling
        "[off,3,1]",    // min > max on an off location
        "[opp-off,-1,-3]", // min > max on the opponent's off location
        "[off,1,] [off,,3]",          // duplicate off location
        "[opp-off,,-1] [opp-off,,-2]",// duplicate opp-off location
        "[off,1,] [OFF,2,]",          // duplicate off location, spelled in mixed case
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
