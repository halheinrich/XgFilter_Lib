using BgDataTypes_Lib;
using BgDataTypes_Lib.TestSupport;
using XgFilter_Lib.Enums;
using XgFilter_Lib.Filtering;
using XgFilter_Lib.Tests.Helpers;

namespace XgFilter_Lib.Tests.Filtering;

public class PlayTypeFilterTests
{
    // -----------------------------------------------------------------------
    //  Fixtures — a 4-4 from the standard start, where 24/20(2) makes the
    //  decision-maker's 20-point. The after-boards are the records' own
    //  derivation from these candidates, never stated here.
    // -----------------------------------------------------------------------

    /// <summary>24/20(2) 13/9(2): makes the 20-point.</summary>
    private static readonly Play MakesTwenty = [new(24, 20), new(24, 20), new(13, 9), new(13, 9)];

    /// <summary>13/9(2) 6/2(2): leaves the back checkers on the 24.</summary>
    private static readonly Play StaysBack = [new(13, 9), new(13, 9), new(6, 2), new(6, 2)];

    /// <summary>
    /// A 4-4 at <paramref name="board"/> (the standard start by default) with
    /// the given candidates, the first the best by equity, and the player's
    /// move at <paramref name="userPlayIndex"/> (null: off the list, with the
    /// analyser's error stated).
    /// </summary>
    private static CheckerPlayDecision FourFour(
        IReadOnlyList<PlayCandidate> candidates, int? userPlayIndex, BoardPosition? board = null) =>
        TestRecords.CheckerPlay(
            position: TestRecords.Position(mop: board),
            decision: TestRecords.CheckerPlayData(
                dice: [4, 4],
                plays: candidates,
                userPlayIndex: userPlayIndex,
                unlistedPlayError: userPlayIndex is null ? 0.1 : null));

    private static PlayCandidate Candidate(Play play, double equity) =>
        TestRecords.Candidate(play: play, equity: equity);

    // -----------------------------------------------------------------------
    //  Empty type set
    // -----------------------------------------------------------------------

    [Fact]
    public void EmptyTypes_CheckerPlay_ReturnsFalse()
    {
        var filter = new PlayTypeFilter([]);
        var bestMakesPlayerDoesNot = FourFour([Candidate(MakesTwenty, 0.1), Candidate(StaysBack, 0.0)], userPlayIndex: 1);
        AssertMatchesBoth(filter, bestMakesPlayerDoesNot, expected: false);
    }

    // -----------------------------------------------------------------------
    //  No after-board, and none substituted
    // -----------------------------------------------------------------------

    [Fact]
    public void CubeDecision_SelectedType_ReturnsFalse()
    {
        // No play is made, so neither after-board exists.
        var filter = new PlayTypeFilter([PlayType.Make20Pt]);
        AssertMatchesBoth(filter, TestRecords.Cube(), expected: false);
    }

    [Fact]
    public void OffListPlay_NoPlayerAfterBoard_ReturnsFalse()
    {
        // The best play makes the 20-point, and the player's move is off the
        // candidate list, so it has no after-board (null, halheinrich/backgammon#15).
        // The shape of a play that was not listed is unknown: the filter
        // drops it rather than reading another board in its place. Reading
        // the board before the play, or the empty board, as the player's
        // would pass it here (neither has the decision-maker on the 20).
        var filter = new PlayTypeFilter([PlayType.Make20Pt]);
        var offList = FourFour([Candidate(MakesTwenty, 0.1), Candidate(StaysBack, 0.0)], userPlayIndex: null);

        offList.AfterPlayerBoard.Should().BeNull("the premise: an off-list play has no after-board");
        AssertMatchesBoth(filter, offList, expected: false);
    }

    // -----------------------------------------------------------------------
    //  Make20Pt behavioural coverage
    // -----------------------------------------------------------------------

    [Fact]
    public void Make20Pt_BestMakes_PlayerDoesNot_ReturnsTrue()
    {
        var filter = new PlayTypeFilter([PlayType.Make20Pt]);
        AssertMatchesBoth(filter, FourFour([Candidate(MakesTwenty, 0.1), Candidate(StaysBack, 0.0)], userPlayIndex: 1), expected: true);
    }

    [Fact]
    public void Make20Pt_PlayerMakes_BestDoesNot_ReturnsTrue()
    {
        var filter = new PlayTypeFilter([PlayType.Make20Pt]);
        AssertMatchesBoth(filter, FourFour([Candidate(StaysBack, 0.1), Candidate(MakesTwenty, 0.0)], userPlayIndex: 1), expected: true);
    }

    [Fact]
    public void Make20Pt_PlayerPlaysTheBest_ReturnsFalse()
    {
        // Both after-boards are the same position: neither differentiates.
        var filter = new PlayTypeFilter([PlayType.Make20Pt]);
        AssertMatchesBoth(filter, FourFour([Candidate(MakesTwenty, 0.1), Candidate(StaysBack, 0.0)], userPlayIndex: 0), expected: false);
    }

    [Fact]
    public void Make20Pt_AlreadyMade_ReturnsFalse()
    {
        // The decision-maker already holds the 20-point. The best play breaks
        // it (20/16(2) 13/9(2)) and the player's keeps it — exactly one play
        // "makes" it on the after-boards, which a filter ignoring the prior
        // board would admit.
        var twentyMade = BoardBuilder.Build(
            (1, -2), (6, 5), (8, 3), (12, -5), (13, 5), (17, -3), (19, -5), (20, 2));
        Play breaksTwenty = [new(20, 16), new(20, 16), new(13, 9), new(13, 9)];

        var filter = new PlayTypeFilter([PlayType.Make20Pt]);
        AssertMatchesBoth(
            filter,
            FourFour([Candidate(breaksTwenty, 0.1), Candidate(StaysBack, 0.0)], userPlayIndex: 1, board: twentyMade),
            expected: false);
    }

    // -----------------------------------------------------------------------
    //  The ranking in force: the best play's after-board is the ranking's
    // -----------------------------------------------------------------------

    [Fact]
    public void Make20Pt_BestPlayIsTheRankings()
    {
        // 24/20(2) 13/9(2) at 4-ply, +0.25, against 13/9(2) 6/2(2) at 2-ply,
        // +0.50, which the player chose. Under equity the player played the
        // best, so nothing differentiates; under depth first the best is the
        // deeper play, which makes the 20-point the player's does not.
        var record = FourFour(
            [
                TestRecords.Candidate(play: MakesTwenty, analysisLevel: AnalysisLevel.Ply4, equity: 0.25),
                TestRecords.Candidate(play: StaysBack, analysisLevel: AnalysisLevel.Ply2, equity: 0.50),
            ],
            userPlayIndex: 1);

        var filter = new PlayTypeFilter([PlayType.Make20Pt]);
        AssertMatchesBoth(filter, record, expected: false, PlayRanking.Equity);
        AssertMatchesBoth(filter, record, expected: true, PlayRanking.DepthFirst);
    }

    // -----------------------------------------------------------------------
    //  Unknown enum value — fails fast at construction
    // -----------------------------------------------------------------------

    [Fact]
    public void UnknownPlayType_Constructor_Throws()
    {
        var act = () => new PlayTypeFilter([(PlayType)999]);
        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void UnknownPlayType_MixedWithValid_Constructor_Throws()
    {
        var act = () => new PlayTypeFilter([PlayType.Make20Pt, (PlayType)999]);
        act.Should().Throw<ArgumentOutOfRangeException>();
    }
}
