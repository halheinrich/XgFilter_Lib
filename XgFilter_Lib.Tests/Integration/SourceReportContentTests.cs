using BgDataTypes_Lib;
using BgDataTypes_Lib.TestSupport;
using ConvertXgToJson_Lib;
using ConvertXgToJson_Lib.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using XgFilter_Lib.Filtering;
using XgFilter_Lib.Tests.Helpers;

namespace XgFilter_Lib.Tests.Integration;

/// <summary>
/// What a walk left out of the sources it decoded, as data
/// (halheinrich/backgammon#514): <see cref="SourceReport.Omissions"/> and
/// <see cref="SourceReport.ForcedPlayCount"/>, facts about content that sit
/// beside the report's facts about decoding and never disturb them. Every walk
/// runs over sources synthesized through the producer's writers
/// (<see cref="SyntheticSources"/>) and read back through the real read path,
/// so these gate; one test reads Hal's three files and carries
/// <c>RequiresFixtureFiles</c>. The walk order of a directory is the file
/// system's, so an assertion on order runs over streams.
/// </summary>
public class SourceReportContentTests
{
    private static readonly ILogger<FilteredDecisionIterator> NullLogger =
        NullLogger<FilteredDecisionIterator>.Instance;

    private static FilteredDecisionIterator NewIterator(DecisionFilterSet? filters = null) =>
        new(filters ?? new DecisionFilterSet(), PlayRanking.Equity, NullLogger);

    // -----------------------------------------------------------------------
    //  The sources: one game per entry
    // -----------------------------------------------------------------------

    /// <summary>One game of <see cref="Match"/>: what is recorded in it, whether its header is refused, and which move of it, if any, holds a corrupt candidate.</summary>
    private sealed record Game(Action<XgGameBuilder> Record, bool IsRefused = false, int? CorruptMove = null);

    /// <summary>A game of <paramref name="count"/> decisions.</summary>
    private static Game Moves(int count) => new(game => game.WithMoves(count));

    /// <summary>A game of <paramref name="count"/> forced plays, and no decision.</summary>
    private static Game Forced(int count) => new(game => game.WithForcedPlays(count));

    /// <summary>A game the walk omits whole for its standing.</summary>
    private static Game Refused { get; } = new(game => game.WithMoves(1), IsRefused: true);

    /// <summary>Moves 1 and 3 are decisions; move 2 carries XG's illegal-play marker.</summary>
    private static Game AroundAnIllegalPlay { get; } = new(game => game
        .WithMoves(1)
        .IllegalPlay(XgPlayer.Player2, new DiceRoll(5, 2))
        .Play(XgPlayer.Player2, new DiceRoll(3, 1), [new(8, 5), new(6, 5)]));

    /// <summary>Moves 1 and 3 are decisions; move 2, a 3-1, holds a corrupt candidate.</summary>
    private static Game AroundACorruptCandidate { get; } = new(game => game.WithMoves(3), CorruptMove: 2);

    /// <summary>A 5-point match of the <paramref name="games"/>, numbered in order.</summary>
    private static XgFile Match(params Game[] games)
    {
        var builder = XgFileBuilder.ForMatch(5, "P1", "P2");
        foreach (var game in games)
            game.Record(builder.AddGame());

        var refused = games.Index().Where(g => g.Item.IsRefused).Select(g => g.Index + 1).ToArray();
        var file = SyntheticSources.WithRefusedGames(builder.Build(), refused);
        foreach (var (index, game) in games.Index())
            if (game.CorruptMove is int move)
                file = SyntheticSources.WithCorruptCandidate(file, index + 1, move);
        return file;
    }

    /// <summary>What the producer records for <paramref name="file"/> alone: the walk's facts, unfolded.</summary>
    private static IReadOnlyList<XgOmission> ProducerOmissions(XgFile file, string name, out int forcedPlays)
    {
        var state = new XgIteratorState();
        _ = XgDecisionIterator.IterateDiagramRequests(file, name, state).ToList();
        forcedPlays = state.ForcedPlayCount;
        return state.Omissions;
    }

    // -----------------------------------------------------------------------
    //  Every entry point, both shapes
    // -----------------------------------------------------------------------

    public static TheoryData<string> Walks() =>
    [
        nameof(FilteredDecisionIterator.IterateXgDirectory),
        nameof(FilteredDecisionIterator.IterateJsonDirectory),
        nameof(FilteredDecisionIterator.IterateXgDirectoryDiagrams),
        nameof(FilteredDecisionIterator.IterateXgStreams),
        nameof(FilteredDecisionIterator.IterateXgStreamDiagrams),
    ];

    public static TheoryData<string> XgFormatWalks() =>
    [
        nameof(FilteredDecisionIterator.IterateXgDirectory),
        nameof(FilteredDecisionIterator.IterateXgDirectoryDiagrams),
        nameof(FilteredDecisionIterator.IterateXgStreams),
        nameof(FilteredDecisionIterator.IterateXgStreamDiagrams),
    ];

    /// <summary>The two shapes of the stream walk, for what needs a known walk order.</summary>
    public static TheoryData<bool> Shapes() => [false, true];

    /// <summary><paramref name="name"/> as <paramref name="walk"/> can read it: the JSON directory walk takes <c>.json</c>.</summary>
    private static string NameIn(string walk, string name) =>
        walk == nameof(FilteredDecisionIterator.IterateJsonDirectory) ? Path.ChangeExtension(name, ".json") : name;

    /// <summary>
    /// Runs <paramref name="walk"/> to its end over <paramref name="sources"/>,
    /// each written in the format the walk reads and named by
    /// <see cref="NameIn"/>, a null file standing for one nothing can decode.
    /// Returns the ids of the decisions yielded.
    /// </summary>
    private static List<string> Run(
        string walk, FilteredDecisionIterator iterator, SourceReport report,
        params (string Name, XgFile? File)[] sources)
    {
        bool json = walk == nameof(FilteredDecisionIterator.IterateJsonDirectory);
        var written = sources
            .Select(s => (Name: NameIn(walk, s.Name), Bytes: s.File is null
                ? []
                : json ? SyntheticSources.JsonBytes(s.File) : SyntheticSources.XgBytes(s.File)))
            .ToList();

        var streams = written.Select(s => new XgFileStream(s.Name, new MemoryStream(s.Bytes)));
        if (walk == nameof(FilteredDecisionIterator.IterateXgStreams))
            return [.. iterator.IterateXgStreams(streams, report).Select(r => r.Id.ToString())];
        if (walk == nameof(FilteredDecisionIterator.IterateXgStreamDiagrams))
            return [.. iterator.IterateXgStreamDiagrams(streams, report).Select(d => d.Id.ToString())];

        var directory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(directory);
        try
        {
            foreach (var (name, bytes) in written)
                File.WriteAllBytes(Path.Combine(directory, name), bytes);

            return walk switch
            {
                nameof(FilteredDecisionIterator.IterateXgDirectory) =>
                    [.. iterator.IterateXgDirectory(directory, report).Select(r => r.Id.ToString())],
                nameof(FilteredDecisionIterator.IterateXgDirectoryDiagrams) =>
                    [.. iterator.IterateXgDirectoryDiagrams(directory, report).Select(d => d.Id.ToString())],
                nameof(FilteredDecisionIterator.IterateJsonDirectory) =>
                    [.. iterator.IterateJsonDirectory(directory, report).Select(r => r.Id.ToString())],
                _ => throw new ArgumentOutOfRangeException(nameof(walk), walk, "Not a walk."),
            };
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    /// <summary>The stream walk in one shape, lazily, so a test decides how far it goes.</summary>
    private static IEnumerable<string> StreamWalk(
        bool diagrams, FilteredDecisionIterator iterator, SourceReport report,
        params (string Name, XgFile File)[] sources)
    {
        var streams = sources.Select(s => SyntheticSources.XgEntry(s.Name, s.File)).ToList();
        return diagrams
            ? iterator.IterateXgStreamDiagrams(streams, report).Select(d => d.Id.ToString())
            : iterator.IterateXgStreams(streams, report).Select(r => r.Id.ToString());
    }

    [Theory]
    [MemberData(nameof(Walks))]
    public void ARefusedGameBetweenValidOnes_ThenANextSource_KeepsTheDecisionsAroundIt_AndNamesItsSource(string walk)
    {
        var refused = Match(Moves(2), Refused, Moves(2));
        var next = Match(Moves(2));
        var report = new SourceReport();

        var ids = Run(walk, NewIterator(), report, ("refused.xg", refused), ("next.xg", next));

        string name = NameIn(walk, "refused.xg");
        var unchanged = XgDecisionIterator.IterateDiagramRequests(Match(Moves(2), Moves(2), Moves(2)), name)
            .Where(d => d.Game != 2)
            .Select(d => d.Id.ToString());
        var nextIds = SyntheticSources.ProducerIds(next, NameIn(walk, "next.xg")).Select(id => id.ToString());
        ids.Should().BeEquivalentTo(unchanged.Concat(nextIds), "games 1 and 3 keep their numbers, and the next source is untouched");

        var omission = report.Omissions.Should().ContainSingle().Subject;
        omission.Should().Be(ProducerOmissions(refused, name, out _).Single(), "the report keeps the producer's omission, complete reason and all");
        omission.Game.Should().Be(2);
        omission.Kind.Should().Be(XgOmissionKind.InvalidStanding);
        report.Rejected.Should().BeEmpty();
        report.ReadableCount.Should().Be(2);
        report.IsComplete.Should().BeTrue();
    }

    [Theory]
    [MemberData(nameof(Walks))]
    public void ASourceWithEveryGameRefused_IsReadable_NotRejected_AndKeepsItsOmissions(string walk)
    {
        var report = new SourceReport();

        var ids = Run(walk, NewIterator(), report, ("refused.xg", Match(Refused, Refused)));

        ids.Should().BeEmpty();
        report.Omissions.Select(o => o.Game).Should().Equal(1, 2);
        report.ReadableCount.Should().Be(1);
        report.Rejected.Should().BeEmpty();
        report.IsComplete.Should().BeTrue();
        report.AllRejected.Should().BeFalse();
    }

    [Theory]
    [MemberData(nameof(Walks))]
    public void AWalkOfOnlySourcesWithEveryGameRefused_IsNotAllRejected(string walk)
    {
        var report = new SourceReport();

        var ids = Run(walk, NewIterator(), report,
            ("one.xg", Match(Refused)), ("two.xg", Match(Refused, Refused)));

        ids.Should().BeEmpty();
        report.Omissions.Should().HaveCount(3);
        report.AttemptedCount.Should().Be(2);
        report.ReadableCount.Should().Be(2);
        report.AllRejected.Should().BeFalse("those sources were read: what they lack is decisions, not decoding");
    }

    [Theory]
    [MemberData(nameof(Walks))]
    public void ACorruptCandidate_IsAnOmission_WithTheDecisionsAroundItKept(string walk)
    {
        var report = new SourceReport();

        var ids = Run(walk, NewIterator(), report, ("corrupt.xg", Match(AroundACorruptCandidate)));

        ids.Should().HaveCount(2);
        var omission = report.Omissions.Should().ContainSingle().Subject;
        omission.Kind.Should().Be(XgOmissionKind.CorruptCandidate);
        omission.Game.Should().Be(1);
        omission.MoveNumber.Should().Be(2);
        omission.Roll.Should().Be(new DiceRoll(3, 1));
        report.ReadableCount.Should().Be(1);
    }

    [Theory]
    [MemberData(nameof(Walks))]
    public void AnIllegalPlay_PassesBySilently_WithTheDecisionsAroundItKept(string walk)
    {
        // Hal's rulings: "Skip decisions where an illegal play was made"; "Do
        // not report illegal plays; they occur and are legit."
        var file = Match(AroundAnIllegalPlay);
        var logger = new RecordingLogger<FilteredDecisionIterator>();
        var iterator = new FilteredDecisionIterator(new DecisionFilterSet(), PlayRanking.Equity, logger);
        var report = new SourceReport();

        var ids = Run(walk, iterator, report, ("illegal.xg", file));

        var around = XgDecisionIterator.IterateDiagramRequests(file, NameIn(walk, "illegal.xg")).ToList();
        around.Select(d => d.MoveNumber).Should().Equal(1, 3);
        ids.Should().Equal(around.Select(d => d.Id.ToString()));
        report.Omissions.Should().BeEmpty();
        report.ForcedPlayCount.Should().Be(0);
        logger.Entries.Should().BeEmpty();
        report.ReadableCount.Should().Be(1);
        report.Rejected.Should().BeEmpty();
    }

    [Theory]
    [MemberData(nameof(Walks))]
    public void AForcedOnlySource_IsACount_NotAnOmission_AndNotADecision(string walk)
    {
        var report = new SourceReport();

        var ids = Run(walk, NewIterator(), report, ("forced.xg", Match(Forced(3))));

        ids.Should().BeEmpty();
        report.ForcedPlayCount.Should().Be(3);
        report.Omissions.Should().BeEmpty();
        report.ReadableCount.Should().Be(1);
        report.AllRejected.Should().BeFalse();
    }

    [Theory]
    [MemberData(nameof(XgFormatWalks))]
    public void AnXgpWithAForcedPlayAndAnAnalysedCube_YieldsTheCube_AndCountsThePlay(string walk)
    {
        var file = SyntheticSources.PositionWithAForcedPlay();
        var report = new SourceReport();

        var ids = Run(walk, NewIterator(), report, ("position.xgp", file));

        ids.Should().ContainSingle();
        XgDecisionIterator.IterateDiagramRequests(file, "position.xgp")
            .Should().ContainSingle().Which.Kind.Should().Be(DecisionKind.Cube);
        report.ForcedPlayCount.Should().Be(1);
        report.Omissions.Should().BeEmpty();
    }

    [Theory]
    [MemberData(nameof(Walks))]
    public void ARejectedSourceAndAnOmittedGame_AreKeptApart(string walk)
    {
        var report = new SourceReport();

        Run(walk, NewIterator(), report, ("malformed.xg", null), ("refused.xg", Match(Moves(1), Refused)));

        report.Rejected.Select(r => r.SourceName).Should().Equal(NameIn(walk, "malformed.xg"));
        report.Omissions.Select(o => o.SourceFile).Should().Equal(NameIn(walk, "refused.xg"));
        report.AttemptedCount.Should().Be(2);
        report.ReadableCount.Should().Be(1);
        report.AllRejected.Should().BeFalse();
    }

    // -----------------------------------------------------------------------
    //  Truthful about the walk: what was examined, and no more
    // -----------------------------------------------------------------------

    [Theory]
    [MemberData(nameof(Shapes))]
    public void AConsumerThatStopsEarly_HoldsWhatWasExamined_AndNothingLater(bool diagrams)
    {
        var first = Match(Forced(1), Refused, Moves(2), Forced(3), Refused);
        var report = new SourceReport();

        _ = StreamWalk(diagrams, NewIterator(), report, ("first.xg", first), ("second.xg", Match(Refused))).First();

        report.Omissions.Select(o => (o.SourceFile, o.Game)).Should().Equal([("first.xg", 2)],
            "game 5 and the second source were never reached, and stopping reads on to neither");
        report.ForcedPlayCount.Should().Be(1, "game 4's forced plays were never reached");
        report.AttemptedCount.Should().Be(1);
        report.IsComplete.Should().BeFalse();
        report.AllRejected.Should().BeFalse();
    }

    [Theory]
    [MemberData(nameof(Shapes))]
    public void TheReportReadAtAYield_HoldsEverythingExaminedSoFar(bool diagrams)
    {
        var file = Match(Moves(1), Refused, Forced(2), Moves(1), Refused, Forced(1), Moves(1));
        var report = new SourceReport();
        var atEachYield = new List<(int Omissions, int Forced)>();

        foreach (var _ in StreamWalk(diagrams, NewIterator(), report, ("match.xg", file)))
            atEachYield.Add((report.Omissions.Count, report.ForcedPlayCount));

        atEachYield.Should().Equal((0, 0), (1, 2), (2, 3));
    }

    [Theory]
    [MemberData(nameof(Shapes))]
    public void WhatLiesAfterASourcesLastYieldedDecision_IsKept(bool diagrams)
    {
        var report = new SourceReport();

        var ids = StreamWalk(diagrams, NewIterator(), report, ("match.xg", Match(Moves(1), Refused, Forced(2)))).ToList();

        ids.Should().ContainSingle();
        report.Omissions.Select(o => o.Game).Should().Equal(2);
        report.ForcedPlayCount.Should().Be(2);
        report.IsComplete.Should().BeTrue();
    }

    [Theory]
    [MemberData(nameof(Shapes))]
    public void AFilterThatThrows_LeavesWhatWasFoundSinceThePreviousYield_AndPropagates(bool diagrams)
    {
        var file = Match(Moves(1), Refused, Forced(2), Moves(1), Refused);
        var iterator = NewIterator(new DecisionFilterSet().Add(new ThrowsAfter(1)));
        var report = new SourceReport();
        int yielded = 0;

        var act = () =>
        {
            foreach (var _ in StreamWalk(diagrams, iterator, report, ("match.xg", file)))
                yielded++;
        };

        act.Should().Throw<InvalidOperationException>().WithMessage(ThrowsAfter.Failure);
        yielded.Should().Be(1);
        report.Omissions.Select(o => o.Game).Should().Equal([2], "game 2 was examined after the last yield; game 5 never was");
        report.ForcedPlayCount.Should().Be(2);
        report.IsComplete.Should().BeFalse();
    }

    [Fact]
    public void ALoggerThatThrows_LeavesTheOmissionItWasLogging_AndPropagates()
    {
        // The producer records before it logs, as the walk records a rejection
        // before its trace.
        var iterator = new FilteredDecisionIterator(
            new DecisionFilterSet(), PlayRanking.Equity, new ThrowingLogger<FilteredDecisionIterator>());
        var report = new SourceReport();

        var act = () => StreamWalk(false, iterator, report, ("match.xg", Match(Refused))).ToList();

        act.Should().Throw<InvalidOperationException>().WithMessage($"*{ThrowingLogger<FilteredDecisionIterator>.Failure}*");
        report.Omissions.Should().ContainSingle();
        report.IsComplete.Should().BeFalse();
    }

    [Theory]
    [MemberData(nameof(Shapes))]
    public void ASourceWhoseEveryDecisionTheFilterRejects_StillRecordsWhatItHeld(bool diagrams)
    {
        // Nothing is yielded, so only leaving the source can bring the report
        // up to date.
        var iterator = NewIterator(new DecisionFilterSet().Add(new MatchNothing()));
        var report = new SourceReport();

        var ids = StreamWalk(diagrams, iterator, report, ("match.xg", Match(Moves(2), Refused, Forced(2), Moves(1)))).ToList();

        ids.Should().BeEmpty();
        report.Omissions.Select(o => o.Game).Should().Equal(2);
        report.ForcedPlayCount.Should().Be(2);
        report.ReadableCount.Should().Be(1);
    }

    [Theory]
    [MemberData(nameof(Shapes))]
    public void AGameAFilterSkips_SaysNothing_AndDecodingIsUnchanged(bool diagrams)
    {
        // 0-0 passes the filter and 1-0 does not, so game 2 is skipped at its
        // header. A refused header reaches no filter, so game 3 still reports.
        var builder = XgFileBuilder.ForMatch(5, "P1", "P2");
        builder.AddGame().WithForcedPlays(1);
        builder.AddGame(score1: 1).WithMoves(1).WithForcedPlays(2);
        builder.AddGame().WithMoves(1);
        var file = SyntheticSources.WithRefusedGames(SyntheticSources.WithCorruptCandidate(builder.Build(), game: 2, move: 1), 3);
        var filters = new FilterConfig { MatchScores = { "5a5a" } }.Build();
        var everything = new SourceReport();
        var skipping = new SourceReport();

        _ = StreamWalk(diagrams, NewIterator(), everything, ("match.xg", file)).ToList();
        _ = StreamWalk(diagrams, NewIterator(filters), skipping, ("match.xg", file)).ToList();

        everything.ForcedPlayCount.Should().Be(3, "the control: unfiltered, the walk examines game 2");
        everything.Omissions.Select(o => o.Game).Should().Equal(2, 3);
        skipping.ForcedPlayCount.Should().Be(1);
        skipping.Omissions.Select(o => o.Game).Should().Equal(3);
        skipping.ReadableCount.Should().Be(everything.ReadableCount, "decoding does not depend on the filters");
        skipping.IsComplete.Should().BeTrue();
    }

    [Theory]
    [MemberData(nameof(Shapes))]
    public void TheRestOfAGameAfterAFiltersStop_SaysNothing(bool diagrams)
    {
        // The filter votes to advance on move 2, the first past its maximum,
        // so the producer never reaches the forced plays at the game's end.
        var file = Match(new Game(game => game.WithMoves(3).WithForcedPlays(2)));
        var filters = new DecisionFilterSet().Add(new MoveNumberFilter(max: 1));
        var everything = new SourceReport();
        var stopping = new SourceReport();

        _ = StreamWalk(diagrams, NewIterator(), everything, ("match.xg", file)).ToList();
        _ = StreamWalk(diagrams, NewIterator(filters), stopping, ("match.xg", file)).ToList();

        everything.ForcedPlayCount.Should().Be(2, "the control: unfiltered, the walk examines the whole game");
        stopping.ForcedPlayCount.Should().Be(0);
    }

    // -----------------------------------------------------------------------
    //  Each update adds only what is new
    // -----------------------------------------------------------------------

    [Theory]
    [MemberData(nameof(Shapes))]
    public void TwoSources_NeitherLeakNorCountTwice(bool diagrams)
    {
        // Several yields after each find, so a report that folded a source's
        // running totals at every update would count them again.
        var first = Match(Forced(2), Refused, Moves(3), Refused, Moves(2));
        var second = Match(Moves(3), Forced(3), Moves(3), AroundACorruptCandidate, Moves(1));
        var report = new SourceReport();

        _ = StreamWalk(diagrams, NewIterator(), report, ("first.xg", first), ("second.xg", second)).ToList();

        var firstOmissions = ProducerOmissions(first, "first.xg", out int firstForced);
        var secondOmissions = ProducerOmissions(second, "second.xg", out int secondForced);
        report.Omissions.Should().Equal(firstOmissions.Concat(secondOmissions));
        report.ForcedPlayCount.Should().Be(firstForced + secondForced);
        report.ForcedPlayCount.Should().Be(5);
        report.Omissions.Should().HaveCount(3);
    }

    [Fact]
    public void ASecondSourcesBookkeeping_StartsAfresh_EvenBehindALargerFirstSource()
    {
        // The first source's forced plays and omissions outnumber the
        // second's, which a shared mark would swallow.
        var report = new SourceReport();

        _ = StreamWalk(false, NewIterator(), report,
            ("first.xg", Match(Forced(3), Refused, Refused)), ("second.xg", Match(Forced(1), Refused))).ToList();

        report.ForcedPlayCount.Should().Be(4);
        report.Omissions.Select(o => (o.SourceFile, o.Game)).Should().Equal(
            ("first.xg", 2), ("first.xg", 3), ("second.xg", 2));
    }

    [Theory]
    [MemberData(nameof(Shapes))]
    public void AnOmissionsListKeptDuringAWalk_StaysAsItWas_AndALaterReadReturnsMore(bool diagrams)
    {
        // Two omissions within one source: exposing the observer's growing
        // list would change the list the consumer kept.
        var file = Match(Refused, Moves(2), Refused, Moves(1));
        var report = new SourceReport();
        IReadOnlyList<XgOmission>? atFirstYield = null;

        foreach (var _ in StreamWalk(diagrams, NewIterator(), report, ("match.xg", file)))
            atFirstYield ??= report.Omissions;

        atFirstYield.Should().NotBeNull();
        atFirstYield!.Select(o => o.Game).Should().Equal(1);
        report.Omissions.Select(o => o.Game).Should().Equal(1, 3);
    }

    [Fact]
    public void ARefusedSecondWalk_AddsNothing()
    {
        var report = new SourceReport();
        var iterator = NewIterator();
        var file = Match(Forced(1), Refused);
        _ = StreamWalk(false, iterator, report, ("match.xg", file)).ToList();

        var second = () => StreamWalk(false, iterator, report, ("match.xg", file)).ToList();

        second.Should().Throw<InvalidOperationException>();
        report.Omissions.Should().ContainSingle();
        report.ForcedPlayCount.Should().Be(1);
    }

    // -----------------------------------------------------------------------
    //  A caller with no report
    // -----------------------------------------------------------------------

    [Theory]
    [MemberData(nameof(Shapes))]
    public void NoReport_YieldsTheSameDecisions_AndTheProducersWarningIsLoggedOnce(bool diagrams)
    {
        var file = Match(Moves(1), Refused, Moves(1));
        var silent = new RecordingLogger<FilteredDecisionIterator>();
        var reporting = new RecordingLogger<FilteredDecisionIterator>();
        var withoutReport = new FilteredDecisionIterator(new DecisionFilterSet(), PlayRanking.Equity, silent);
        var withReport = new FilteredDecisionIterator(new DecisionFilterSet(), PlayRanking.Equity, reporting);
        var report = new SourceReport();

        var entry = SyntheticSources.XgEntry("match.xg", file);
        var plain = diagrams
            ? withoutReport.IterateXgStreamDiagrams([entry]).Select(d => d.Id.ToString()).ToList()
            : withoutReport.IterateXgStreams([entry]).Select(r => r.Id.ToString()).ToList();
        var reported = StreamWalk(diagrams, withReport, report, ("match.xg", file)).ToList();

        plain.Should().HaveCount(2).And.Equal(reported);
        silent.Entries.Should().ContainSingle(e => e.Level == LogLevel.Warning).Which.Message.Should().Contain("game 2");
        reporting.Entries.Should().ContainSingle(e => e.Level == LogLevel.Warning, "the report adds no second line");
        reporting.Entries.Single().Message.Should().Be(silent.Entries.Single().Message);
    }

    // -----------------------------------------------------------------------
    //  Hal's three files (halheinrich/backgammon#514), local-only
    // -----------------------------------------------------------------------

    [Fact]
    [Trait("Category", "RequiresFixtureFiles")]
    public void HalsThreeFilesInOneWalk_ReportFiveOmittedGames_AndNothingRejected()
    {
        const string Hardigree = "2024-07-03_Jonathan_Hardigree_vs_Michael_Mesich_29046.xg";
        const string Norberg = "2024-11-30_Johan_Norberg_vs_Konrad_Fröschl_34083.xg";
        const string Mtch2987 = "MTCH2987.xg";
        string[] files = [Hardigree, Norberg, Mtch2987];
        var streams = files.Select(name => new XgFileStream(
            name, new MemoryStream(File.ReadAllBytes(Path.Combine(TestPaths.FixtureFilesDir, name)))));
        var report = new SourceReport();

        var decisions = NewIterator().IterateXgStreamDiagrams(streams, report).ToList();

        decisions.Should().NotBeEmpty();
        report.Omissions.Select(o => (o.SourceFile, o.Game)).Should().Equal(
            (Hardigree, 7), (Norberg, 9), (Mtch2987, 1), (Mtch2987, 2), (Mtch2987, 3));
        report.Omissions.Should().OnlyContain(o => o.Kind == XgOmissionKind.InvalidStanding);
        report.Rejected.Should().BeEmpty();
        report.ReadableCount.Should().Be(3);
        report.IsComplete.Should().BeTrue();
    }

    // -----------------------------------------------------------------------
    //  Doubles
    // -----------------------------------------------------------------------

    private sealed class MatchNothing : IDecisionFilter
    {
        public bool Matches(IDecisionFilterData data) => false;
    }

    /// <summary>Passes the first <paramref name="passing"/> decisions asked about, and throws on the next.</summary>
    private sealed class ThrowsAfter(int passing) : IDecisionFilter
    {
        public const string Failure = "filter failure";

        private int _asked;

        public bool Matches(IDecisionFilterData data) =>
            _asked++ < passing ? true : throw new InvalidOperationException(Failure);
    }
}
