using ConvertXgToJson_Lib;
using ConvertXgToJson_Lib.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using XgFilter_Lib.Filtering;

namespace XgFilter_Lib.Tests.Integration;

/// <summary>
/// End-to-end MaNa orientation coverage through <see cref="FilteredDecisionIterator"/>.
/// The shipped 4a5a bug: game headers are player1/player2-anchored while score
/// tokens are on-roll anchored, so a decision whose on-roll player is the
/// file's player 2 was eaten by the game-level skip (and, mid-.xg, by the
/// match-advance cut) before <c>Matches</c> ever saw it.
///
/// <para>
/// Files are synthesized inline as real XG binary containers
/// (<see cref="XgFileWriter"/>) and fed through
/// <see cref="FilteredDecisionIterator.IterateXgStreams"/>, so the whole
/// producer pipeline is exercised — match header, game-header skip callback,
/// per-row advance callbacks, and the <c>.xgp</c> single-decision policy
/// (which keys off the stream's file extension). Synthetics are regenerable
/// garbage per standing practice; nothing here pins <c>TestData</c> contents.
/// </para>
/// </summary>
public class MatchScoreFilterIntegrationTests
{
    private static readonly ILogger<FilteredDecisionIterator> NullLogger =
        NullLogger<FilteredDecisionIterator>.Instance;

    private static FilteredDecisionIterator NewIterator(params string[] scores) =>
        new(new DecisionFilterSet().Add(new MatchScoreFilter(scores)), NullLogger);

    /// <summary>
    /// Serializes <paramref name="file"/> to real XG binary bytes and wraps
    /// it as a named stream, ready for <c>IterateXgStreams</c>.
    /// </summary>
    private static XgFileStream ToStream(XgFile file, string fileName) =>
        new(fileName, new MemoryStream(XgFileWriter.ToBytes(file)));

    /// <summary>
    /// A 5-point match with one game at score (Score1=0, Score2=1) — player 1
    /// is 5-away, player 2 is 4-away — carrying one analysed cube decision per
    /// player, player 1's first. On-roll tuples: player 1's decision is (5,4)
    /// ("5a4a"), player 2's is (4,5) ("4a5a").
    /// </summary>
    private static XgFile BuildTwoOrientationMatch() => new()
    {
        Records =
        {
            new MatchHeaderRecord
            {
                EntryType = RecordType.HeaderMatch,
                MatchLength = 5,
                Player1 = "P1",
                Player2 = "P2",
            },
            new GameHeaderRecord
            {
                EntryType = RecordType.HeaderGame,
                Score1 = 0,   // player 1: 5-away
                Score2 = 1,   // player 2: 4-away
                GameNumber = 1,
            },
            AnalysedCube(activePlayer: 1),
            AnalysedCube(activePlayer: -1),
        },
    };

    /// <summary>
    /// The .xgp shape of the corpus bug (Move1_0001.xgp): a position file
    /// whose on-roll player is the file's player 2. Same 5-point, (5-away,
    /// 4-away) score; the single analysed decision belongs to player 2, so
    /// its on-roll tuple is (4,5) — "4a5a".
    /// </summary>
    private static XgFile BuildPlayer2OnRollPosition() => new()
    {
        Records =
        {
            new MatchHeaderRecord
            {
                EntryType = RecordType.HeaderMatch,
                MatchLength = 5,
                Player1 = "P1",
                Player2 = "P2",
            },
            new GameHeaderRecord
            {
                EntryType = RecordType.HeaderGame,
                Score1 = 0,
                Score2 = 1,
                GameNumber = 1,
            },
            AnalysedCube(activePlayer: -1),
        },
    };

    /// <summary>
    /// Minimal analysed cube decision (<c>Analysis.Level &gt; 0</c> is the
    /// producer's analysed-cube test). The board content is irrelevant to
    /// match-score filtering; defaults suffice.
    /// </summary>
    private static CubeRecord AnalysedCube(int activePlayer) => new()
    {
        EntryType = RecordType.Cube,
        ActivePlayer = activePlayer,
        CubeValue = 0,
        Analysis = new DoubleActionAnalysis { Level = 1 },
    };

    // -----------------------------------------------------------------------
    //  .xg match files — both players' decisions in one game
    // -----------------------------------------------------------------------

    [Fact]
    public void Xg_OnRollTupleAnchoredToPlayer2_IsYielded()
    {
        // The bug, end-to-end: 4a5a must reach player 2's decision even though
        // (a) the game header reads (Away1=5, Away2=4) — the old ShouldSkipGame
        // compared that orientation only — and (b) player 1's non-matching
        // (5,4) row streams FIRST, where the old ShouldAdvanceMatch voted to
        // cut the file (tuple (4,5) is not reachable in any FUTURE game from
        // (5,4); it is the current game's mirror).
        var rows = NewIterator("4a5a")
            .IterateXgStreams([ToStream(BuildTwoOrientationMatch(), "match.xg")])
            .ToList();

        var row = rows.Should().ContainSingle(
            "the on-roll 4-away decision belongs to player 2 and must survive " +
            "both the game-level skip and the match-advance cut").Which;
        row.Player.Should().Be("P2");
        row.OnRollNeeds.Should().Be(4);
        row.OpponentNeeds.Should().Be(5);
        row.MatchScore.Should().Be("4a5a");
    }

    [Fact]
    public void Xg_OnRollTupleAnchoredToPlayer1_IsYielded()
    {
        var rows = NewIterator("5a4a")
            .IterateXgStreams([ToStream(BuildTwoOrientationMatch(), "match.xg")])
            .ToList();

        var row = rows.Should().ContainSingle().Which;
        row.Player.Should().Be("P1");
        row.MatchScore.Should().Be("5a4a");
    }

    [Fact]
    public void Xg_BothOrientations_YieldBothDecisions()
    {
        var rows = NewIterator("4a5a", "5a4a")
            .IterateXgStreams([ToStream(BuildTwoOrientationMatch(), "match.xg")])
            .ToList();

        rows.Select(r => (r.Player, r.MatchScore)).Should().Equal(
            ("P1", "5a4a"),
            ("P2", "4a5a"));
    }

    [Fact]
    public void Xg_NeitherOrientation_YieldsNothing()
    {
        NewIterator("3a5a")
            .IterateXgStreams([ToStream(BuildTwoOrientationMatch(), "match.xg")])
            .Should().BeEmpty();
    }

    // -----------------------------------------------------------------------
    //  .xgp position files — the corpus shape of the bug
    // -----------------------------------------------------------------------

    [Fact]
    public void Xgp_OnRollTupleAnchoredToPlayer2_IsYielded()
    {
        var rows = NewIterator("4a5a")
            .IterateXgStreams([ToStream(BuildPlayer2OnRollPosition(), "position.xgp")])
            .ToList();

        var row = rows.Should().ContainSingle(
            "a 4a5a filter must admit an .xgp whose on-roll 4-away player is " +
            "the file's player 2").Which;
        row.Player.Should().Be("P2");
        row.MatchScore.Should().Be("4a5a");
    }

    [Fact]
    public void Xgp_MirrorOrientation_YieldsNothing()
    {
        // Exactness in the other direction: the on-roll player needs 4, so
        // 5a4a — on-roll needs 5 — must NOT match this file.
        NewIterator("5a4a")
            .IterateXgStreams([ToStream(BuildPlayer2OnRollPosition(), "position.xgp")])
            .Should().BeEmpty();
    }
}
