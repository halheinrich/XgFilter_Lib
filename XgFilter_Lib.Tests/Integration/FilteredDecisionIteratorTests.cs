using BgDataTypes_Lib;
using ConvertXgToJson_Lib;
using ConvertXgToJson_Lib.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using XgFilter_Lib.Enums;
using XgFilter_Lib.Filtering;
using XgFilter_Lib.Projection;

namespace XgFilter_Lib.Tests.Integration;

/// <summary>
/// Integration test that reads real .xg files and filters by player name.
/// Requires .xg files in TestData\xg relative to the test output directory.
/// </summary>
public class FilteredDecisionIteratorTests
{
    private static readonly string XgDir = Path.Combine(
        AppContext.BaseDirectory, "TestData", "xg");
    private static readonly string FixtureDir = Path.Combine(
        AppContext.BaseDirectory, "TestData", "FixtureFiles");

    private static readonly ILogger<FilteredDecisionIterator> NullLogger =
        NullLogger<FilteredDecisionIterator>.Instance;

    private static FilteredDecisionIterator NewIterator(DecisionFilterSet filters) =>
        new FilteredDecisionIterator(filters, NullLogger);

    [Fact]
    public void TestDataDirectory_Exists_AndContainsXgFiles()
    {
        Directory.Exists(XgDir).Should().BeTrue(
            because: $"TestData\\xg directory should be copied to output: {XgDir}");

        Directory.GetFiles(XgDir, "*.xg")
            .Should().NotBeEmpty(
            because: "TestData\\xg should contain at least one .xg file");
    }

    [Fact]
    public void IterateXgDirectory_FilterByPlayer_ReturnsOnlyMatchingRows()
    {
        var iterator = NewIterator(
            new DecisionFilterSet().Add(new PlayerFilter(["halheinrich"])));

        var rows = iterator.IterateXgDirectory(FixtureDir).ToList();

        rows.Should().NotBeEmpty("expected at least one decision by halheinrich in the test files");
        rows.Should().OnlyContain(r => r.Player.Equals("halheinrich", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void IterateXgDirectory_FilterByPlayer_CustomColumns()
    {
        var iterator = NewIterator(
            new DecisionFilterSet().Add(new PlayerFilter(["halheinrich"])));

        var rows = iterator.IterateXgDirectory(FixtureDir).ToList();

        var selector = new ColumnSelector([
            Column.Player, Column.SourceFile, Column.Game,
            Column.MoveNumber, Column.Roll, Column.Error, Column.Equity]);
        selector.Header.Should().Be("Player,SourceFile,Game,MoveNumber,Roll,Error,Equity");

        rows.Should().NotBeEmpty();
        rows.Should().OnlyContain(r => selector.Serialize(r).Split(',').Length == 7);
    }

    [Fact]
    public void IterateXgDirectory_FilterByRace_ListsDecisions()
    {
        var iterator = NewIterator(
            new DecisionFilterSet().Add(new ContactTypeFilter([ContactType.Race])));

        var rows = iterator.IterateXgDirectory(XgDir).ToList();

        rows.Should().OnlyContain(r => r.Board.Count == 26);
    }

    [Fact]
    public void IterateXgDirectory_FilterByContact_ListsDecisions()
    {
        var iterator = NewIterator(
            new DecisionFilterSet().Add(new ContactTypeFilter([ContactType.Contact])));

        var rows = iterator.IterateXgDirectory(XgDir).ToList();

        rows.Should().OnlyContain(r => r.Board.Count == 26);
    }

    [Fact]
    public void IterateXgDirectory_RaceAndContactAreComplementary()
    {
        var allIterator = NewIterator(new DecisionFilterSet());
        var raceIterator = NewIterator(
            new DecisionFilterSet().Add(new ContactTypeFilter([ContactType.Race])));
        var contactIterator = NewIterator(
            new DecisionFilterSet().Add(new ContactTypeFilter([ContactType.Contact])));

        var all = allIterator.IterateXgDirectory(XgDir).ToList();
        var race = raceIterator.IterateXgDirectory(XgDir).ToList();
        var contact = contactIterator.IterateXgDirectory(XgDir).ToList();

        (race.Count + contact.Count).Should().Be(all.Count,
            "Race and Contact should be mutually exclusive and exhaustive");
    }

    [Fact]
    public void IterateXgDirectory_WhenNoFilesPresent_ReturnsEmpty()
    {
        var emptyDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(emptyDir);

        try
        {
            var iterator = NewIterator(
                new DecisionFilterSet().Add(new PlayerFilter(["halheinrich"])));

            var rows = iterator.IterateXgDirectory(emptyDir).ToList();

            rows.Should().BeEmpty();
        }
        finally
        {
            Directory.Delete(emptyDir);
        }
    }

    // -----------------------------------------------------------------------
    //  Diagram-shape variants
    // -----------------------------------------------------------------------

    [Fact]
    public void IterateXgDirectoryDiagrams_FilterByPlayer_ReturnsOnlyMatchingDiagrams()
    {
        var iterator = NewIterator(
            new DecisionFilterSet().Add(new PlayerFilter(["halheinrich"])));

        var diagrams = iterator.IterateXgDirectoryDiagrams(FixtureDir).ToList();

        diagrams.Should().NotBeEmpty(
            "expected at least one decision by halheinrich in the test files");
        diagrams.Should().OnlyContain(d =>
            d.Descriptive.OnRollName.Equals("halheinrich", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void IterateXgDirectoryDiagrams_AtLeastOneDecisionHasPlays()
    {
        // The diagram form's whole purpose is the per-candidate Plays list.
        // No filter — take everything in the fixture corpus and assert that
        // some checker-play decision yields a non-empty Plays list. Cube
        // decisions may have empty Plays by contract; we just need one
        // non-cube decision to populate.
        var iterator = NewIterator(new DecisionFilterSet());

        var diagrams = iterator.IterateXgDirectoryDiagrams(FixtureDir).ToList();

        diagrams.Should().NotBeEmpty();
        diagrams.Should().Contain(d => d.Decision.Plays.Count > 0,
            "diagram form must populate Plays for checker-play decisions");
    }

    [Fact]
    public void IterateXgDirectoryDiagrams_RowVariant_AndDiagramVariant_AgreeOnPassingDecisions()
    {
        // Same fixture corpus, same filter set, both shapes implement
        // IDecisionFilterData identically — the pass/fail outcome must be
        // identical decision-for-decision. Compare on (SourceFile, MoveNumber,
        // IsCube) which both shapes carry.
        var iterator = NewIterator(
            new DecisionFilterSet().Add(new PlayerFilter(["halheinrich"])));

        var rowKeys = iterator.IterateXgDirectory(FixtureDir)
            .Select(r => (r.SourceFile, r.MoveNumber, r.IsCube))
            .ToList();

        var diagramKeys = iterator.IterateXgDirectoryDiagrams(FixtureDir)
            .Select(d => (d.Descriptive.SourceFile, d.Descriptive.MoveNumber, d.Decision.IsCube))
            .ToList();

        diagramKeys.Should().Equal(rowKeys,
            "row and diagram variants must yield the same decisions in the same order for the same filter set");
    }

    [Fact]
    public void IterateXgDirectoryDiagrams_WhenNoFilesPresent_ReturnsEmpty()
    {
        var emptyDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(emptyDir);

        try
        {
            var iterator = NewIterator(new DecisionFilterSet());

            var diagrams = iterator.IterateXgDirectoryDiagrams(emptyDir).ToList();

            diagrams.Should().BeEmpty();
        }
        finally
        {
            Directory.Delete(emptyDir);
        }
    }

    // -----------------------------------------------------------------------
    //  XG-format directory iteration covers .xg AND .xgp
    // -----------------------------------------------------------------------

    [Fact]
    public void IterateXgDirectory_IncludesXgpPositionFiles()
    {
        // Mirror the parser-side contract: an XG-format directory walk
        // enumerates both *.xg (match files) and *.xgp (position files).
        // Stage a temp dir containing only .xgp fixtures — if the iterator
        // returns rows from there, .xgp enumeration is wired.
        var tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(tempDir);

        try
        {
            // SourceFile carries the extension (e.g. "foo.xgp"), matching the
            // producer's post-Arc-2 contract — see XgDecisionIterator's
            // sourceFile-with-extension requirement for DecisionId stamping.
            var xgpBaseNames = Directory.GetFiles(FixtureDir, "*.xgp")
                .Select(Path.GetFileName)
                .ToHashSet();
            foreach (var src in Directory.GetFiles(FixtureDir, "*.xgp"))
                File.Copy(src, Path.Combine(tempDir, Path.GetFileName(src)));

            var iterator = NewIterator(new DecisionFilterSet());
            var rows = iterator.IterateXgDirectory(tempDir).ToList();

            rows.Should().NotBeEmpty(
                "an XG-format directory walk must include .xgp position files");
            rows.Should().OnlyContain(r => xgpBaseNames.Contains(r.SourceFile!),
                "every row must trace back to one of the .xgp fixtures placed in the temp dir");
        }
        finally
        {
            Directory.Delete(tempDir, recursive: true);
        }
    }

    [Fact]
    public void IterateXgDirectoryDiagrams_IncludesXgpPositionFiles()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(tempDir);

        try
        {
            // SourceFile carries the extension (e.g. "foo.xgp"), matching the
            // producer's post-Arc-2 contract — see XgDecisionIterator's
            // sourceFile-with-extension requirement for DecisionId stamping.
            var xgpBaseNames = Directory.GetFiles(FixtureDir, "*.xgp")
                .Select(Path.GetFileName)
                .ToHashSet();
            foreach (var src in Directory.GetFiles(FixtureDir, "*.xgp"))
                File.Copy(src, Path.Combine(tempDir, Path.GetFileName(src)));

            var iterator = NewIterator(new DecisionFilterSet());
            var diagrams = iterator.IterateXgDirectoryDiagrams(tempDir).ToList();

            diagrams.Should().NotBeEmpty(
                "the diagram-shape iterator must mirror the row-shape iterator's .xgp parity");
            diagrams.Should().OnlyContain(
                d => xgpBaseNames.Contains(d.Descriptive.SourceFile!));
        }
        finally
        {
            Directory.Delete(tempDir, recursive: true);
        }
    }

    // -----------------------------------------------------------------------
    //  Stream / file-list iteration — parity with the directory walk
    // -----------------------------------------------------------------------

    /// <summary>
    /// Loads every XG-format fixture from <paramref name="dir"/> as an
    /// in-memory <see cref="XgFileStream"/>, buffering the bytes so the streams
    /// outlive the lazy enumeration. Fixture-agnostic: whatever .xg/.xgp files
    /// exist are picked up.
    /// </summary>
    private static List<XgFileStream> LoadStreams(string dir) =>
        Directory.GetFiles(dir, "*.xg")
            .Concat(Directory.GetFiles(dir, "*.xgp"))
            .Select(p => new XgFileStream(
                Path.GetFileName(p), new MemoryStream(File.ReadAllBytes(p))))
            .ToList();

    [Fact]
    public void IterateXgStreams_MatchesDirectoryWalk_RowForRow()
    {
        // The new stream entry must yield exactly what the directory walk does
        // for the same corpus and filter set. Read each fixture both ways and
        // compare on identity fields both shapes carry. Fixture-agnostic.
        var iterator = NewIterator(new DecisionFilterSet());

        var fromDir = iterator.IterateXgDirectory(FixtureDir)
            .Select(r => (r.SourceFile, r.MoveNumber, r.IsCube, r.Player))
            .ToList();

        var fromStreams = iterator.IterateXgStreams(LoadStreams(FixtureDir))
            .Select(r => (r.SourceFile, r.MoveNumber, r.IsCube, r.Player))
            .ToList();

        fromStreams.Should().NotBeEmpty(
            "the fixture corpus must yield at least one decision");
        fromStreams.Should().Equal(fromDir,
            "stream iteration must reproduce the directory walk decision-for-decision");
    }

    [Fact]
    public void IterateXgStreamDiagrams_MatchesDirectoryWalk_RowForRow()
    {
        var iterator = NewIterator(new DecisionFilterSet());

        var fromDir = iterator.IterateXgDirectoryDiagrams(FixtureDir)
            .Select(d => (d.Descriptive.SourceFile, d.Descriptive.MoveNumber,
                          d.Decision.IsCube, d.Descriptive.OnRollName))
            .ToList();

        var fromStreams = iterator.IterateXgStreamDiagrams(LoadStreams(FixtureDir))
            .Select(d => (d.Descriptive.SourceFile, d.Descriptive.MoveNumber,
                          d.Decision.IsCube, d.Descriptive.OnRollName))
            .ToList();

        fromStreams.Should().NotBeEmpty();
        fromStreams.Should().Equal(fromDir,
            "stream diagram iteration must reproduce the directory walk decision-for-decision");
    }

    [Fact]
    public void IterateXgStreams_SourceFileCarriesExtension()
    {
        // The whole point of the stream API is that the caller-supplied name
        // (with extension) flows through untouched — DecisionId stamping
        // depends on Path.GetExtension(SourceFile) downstream.
        var iterator = NewIterator(new DecisionFilterSet());

        var rows = iterator.IterateXgStreams(LoadStreams(FixtureDir)).ToList();

        rows.Should().NotBeEmpty();
        rows.Should().OnlyContain(
            r => Path.HasExtension(r.SourceFile),
            "every yielded SourceFile must retain the extension the caller supplied");
    }

    [Fact]
    public void IterateXgStreams_FilterByPlayer_IsHonoured()
    {
        // The filter pipeline must engage identically on the stream path.
        var iterator = NewIterator(
            new DecisionFilterSet().Add(new PlayerFilter(["halheinrich"])));

        var rows = iterator.IterateXgStreams(LoadStreams(FixtureDir)).ToList();

        rows.Should().NotBeEmpty(
            "expected at least one decision by halheinrich in the fixture corpus");
        rows.Should().OnlyContain(
            r => r.Player.Equals("halheinrich", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void IterateXgStreams_EmptyList_ReturnsEmpty()
    {
        var iterator = NewIterator(new DecisionFilterSet());

        iterator.IterateXgStreams([]).Should().BeEmpty();
        iterator.IterateXgStreamDiagrams([]).Should().BeEmpty();
    }

    [Fact]
    public void IterateXgStreams_NullList_Throws()
    {
        var iterator = NewIterator(new DecisionFilterSet());

        // Eager guard — must throw at the call site, before enumeration.
        var act = () => iterator.IterateXgStreams(null!);
        act.Should().Throw<ArgumentNullException>();

        var actDiagrams = () => iterator.IterateXgStreamDiagrams(null!);
        actDiagrams.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void IterateXgStreams_MissingExtension_FailsFast()
    {
        var iterator = NewIterator(new DecisionFilterSet());

        // A name without an extension is a usage error: it would break the
        // downstream .xg/.xgp/.json discrimination. Must throw on enumeration,
        // not silently skip+log like a malformed file.
        var bad = new[] { new XgFileStream("noextension", new MemoryStream([1, 2, 3])) };

        var act = () => iterator.IterateXgStreams(bad).ToList();
        act.Should().Throw<ArgumentException>()
            .WithMessage("*extension*");
    }

    [Fact]
    public void IterateXgStreams_BlankName_FailsFast()
    {
        var iterator = NewIterator(new DecisionFilterSet());

        var bad = new[] { new XgFileStream("   ", new MemoryStream([1, 2, 3])) };

        var act = () => iterator.IterateXgStreams(bad).ToList();
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void IterateXgStreams_MalformedStream_IsSkippedAndLogged_NotThrown()
    {
        // Parity with the directory walk: a stream that fails to PARSE (vs. a
        // malformed NAME) is skipped+logged, never thrown. Mix one garbage
        // .xg stream with the real corpus and assert survival + a warning.
        var spyLogger = new ListLogger<FilteredDecisionIterator>();
        var iterator = new FilteredDecisionIterator(new DecisionFilterSet(), spyLogger);

        var streams = LoadStreams(FixtureDir);
        streams.Add(new XgFileStream("malformed.xg", new MemoryStream([])));

        var rows = iterator.IterateXgStreams(streams).ToList();

        rows.Should().NotBeEmpty("the valid fixtures must still yield rows");

        // The forwarded producer logger may also emit illegal-play warnings from
        // the valid fixtures; the malformed-stream skip is the lone warning that
        // names "malformed.xg" — that stream fails to parse and never reaches the
        // producer, so only the iterator's own skip can mention it.
        var skip = spyLogger.Entries.Should().ContainSingle(
            e => e.Message.Contains("malformed.xg"),
            "the one malformed stream should produce exactly one skip warning naming it")
            .Which;
        skip.Level.Should().Be(LogLevel.Warning);
        skip.Exception.Should().NotBeNull(
            "the skip warning must carry the original exception, not a stringified message");
    }

    // -----------------------------------------------------------------------
    //  Construction guards
    // -----------------------------------------------------------------------

    [Fact]
    public void Constructor_NullFilters_Throws()
    {
        var act = () => new FilteredDecisionIterator(null!, NullLogger);
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void Constructor_NullLogger_Throws()
    {
        var act = () => new FilteredDecisionIterator(new DecisionFilterSet(), null!);
        act.Should().Throw<ArgumentNullException>();
    }

    // -----------------------------------------------------------------------
    //  Malformed-file resilience — folds in T3-5
    // -----------------------------------------------------------------------

    [Fact]
    public void IterateXgDirectory_MalformedFile_IsSkippedAndLogged_IterationContinues()
    {
        // Mix one malformed .xg file with the real fixture corpus. The
        // iterator must skip the malformed file (logging it as a warning)
        // and yield rows from the surviving ones, never throwing. This
        // pins the contract Task 1a establishes: log + continue, never
        // swallow + continue.
        var tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(tempDir);

        try
        {
            // Copy real fixtures so the corpus has at least one valid file.
            foreach (var src in Directory.GetFiles(FixtureDir, "*.xg"))
                File.Copy(src, Path.Combine(tempDir, Path.GetFileName(src)));

            // Drop a zero-byte garbage .xg file alongside.
            var badPath = Path.Combine(tempDir, "malformed.xg");
            File.WriteAllBytes(badPath, []);

            var spyLogger = new ListLogger<FilteredDecisionIterator>();
            var iterator = new FilteredDecisionIterator(new DecisionFilterSet(), spyLogger);

            var rows = iterator.IterateXgDirectory(tempDir).ToList();

            rows.Should().NotBeEmpty("the surviving fixtures should still yield rows");

            // The forwarded producer logger may also emit illegal-play warnings
            // from the surviving fixtures; the malformed-file skip is the lone
            // warning that names "malformed.xg" — that file fails to parse and
            // never reaches the producer, so only the iterator's own skip can
            // mention it.
            var skip = spyLogger.Entries.Should().ContainSingle(
                e => e.Message.Contains("malformed.xg"),
                "the one malformed file should produce exactly one skip warning naming it")
                .Which;
            skip.Level.Should().Be(LogLevel.Warning);
            skip.Exception.Should().NotBeNull(
                "the skip warning must carry the original exception, not a stringified message");
        }
        finally
        {
            Directory.Delete(tempDir, recursive: true);
        }
    }

    // -----------------------------------------------------------------------
    //  Logger forwarding — producer-level warnings surface through the pipeline
    // -----------------------------------------------------------------------

    [Fact]
    public void IterateJsonDirectory_IllegalPlay_ProducerWarningSurfacesThroughSuppliedLogger()
    {
        // The iterator threads its ctor logger into XgDecisionIterator, so a
        // producer-level event — here XG's illegal-play marker — must reach the
        // caller's logger. This is distinct from the iterator's own "Skipping"
        // catch: the "Illegal play" warning originates *inside* the producer and
        // only lands if _logger was forwarded. The message's file/game/move/roll
        // content is pinned by the producer's own tests; here we assert only that
        // the forwarding happens.
        var tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(tempDir);

        try
        {
            // Materialise a synthetic illegal-play file via the library's own
            // JSON round-trip ($type-discriminated SaveRecordConverter) so it
            // flows through the real file-read path rather than bypassing it.
            var json = XgFileReader.ToJson(BuildFileWithIllegalPlay());
            File.WriteAllText(Path.Combine(tempDir, "illegal.json"), json);

            var spyLogger = new ListLogger<FilteredDecisionIterator>();
            var iterator = new FilteredDecisionIterator(new DecisionFilterSet(), spyLogger);

            _ = iterator.IterateJsonDirectory(tempDir).ToList();

            spyLogger.Entries.Should().Contain(
                e => e.Level == LogLevel.Warning && e.Message.Contains("Illegal play"),
                "the producer's illegal-play warning must surface through the iterator's supplied logger");
        }
        finally
        {
            Directory.Delete(tempDir, recursive: true);
        }
    }

    [Fact]
    public void IterateXgDirectory_RealFixtureWithIllegalPlay_ProducerWarningSurfacesThroughSuppliedLogger()
    {
        // Companion to the synthetic test above, on the primary production
        // path: the real binary .xg read rather than the JSON detour. The
        // FixtureFiles corpus includes a tournament file that carries XG's
        // illegal-play marker, so walking it must surface the producer's
        // "Illegal play" warning through the iterator's supplied logger.
        const string knownCarrier =
            "Avi Cohen (6.86) - Max Stockslager (10.55) 2023-02-09_18122.xg";

        var spyLogger = new ListLogger<FilteredDecisionIterator>();
        var iterator = new FilteredDecisionIterator(new DecisionFilterSet(), spyLogger);

        _ = iterator.IterateXgDirectory(FixtureDir).ToList();

        spyLogger.Entries.Should().Contain(
            e => e.Level == LogLevel.Warning
                 && e.Message.Contains("Illegal play")
                 && e.Message.Contains(knownCarrier),
            $"the real fixture '{knownCarrier}' carries an illegal play whose warning must " +
            "surface through the supplied logger on the binary .xg read path");
    }

    // -----------------------------------------------------------------------
    //  Early-exit pipeline — iterator honors filter-set votes
    // -----------------------------------------------------------------------

    [Fact]
    public void ShouldSkipMatch_VotedByFilter_IteratorSkipsEveryFile()
    {
        // Spy filter votes skip-every-match. The iterator must consult
        // ShouldSkipMatch before walking any file's rows; if it doesn't,
        // we'll see rows in the output. Zero rows is the contract.
        var iterator = new FilteredDecisionIterator(
            new DecisionFilterSet().Add(new SkipAllMatchesFilter()), NullLogger);

        iterator.IterateXgDirectory(FixtureDir).Should().BeEmpty(
            "ShouldSkipMatch=true must short-circuit the file before any row is yielded");
    }

    [Fact]
    public void ShouldSkipGame_VotedByFilter_IteratorSkipsEveryGame()
    {
        var iterator = new FilteredDecisionIterator(
            new DecisionFilterSet().Add(new SkipAllGamesFilter()), NullLogger);

        iterator.IterateXgDirectory(FixtureDir).Should().BeEmpty(
            "ShouldSkipGame=true on every game must keep any row from being yielded");
    }

    [Fact]
    public void ShouldAdvanceMatch_VotedByFilter_AtMostOneRowPerSourceFile()
    {
        // Spy filter votes advance-match on the first matching row of any
        // file. If the iterator honors StopMatchAfter, exactly one row
        // per file should reach the consumer. Pin via SourceFile-uniqueness
        // — content-agnostic, robust against fixture corpus changes.
        var iterator = new FilteredDecisionIterator(
            new DecisionFilterSet().Add(new AdvanceMatchOnAnyRowFilter()), NullLogger);

        var rows = iterator.IterateXgDirectory(FixtureDir).ToList();

        rows.Should().NotBeEmpty("the iterator must yield each file's first decision");
        rows.Should().HaveCountGreaterThan(1,
            "the fixture corpus has multiple .xg files; iteration must reach more than one");
        rows.Select(r => r.SourceFile).Should().OnlyHaveUniqueItems(
            "StopMatchAfter=true after a yield must cut the rest of the file");
    }

    [Fact]
    public void ShouldAdvanceGame_VotedByFilter_AtMostOneRowPerGame()
    {
        var iterator = new FilteredDecisionIterator(
            new DecisionFilterSet().Add(new AdvanceGameOnAnyRowFilter()), NullLogger);

        var rows = iterator.IterateXgDirectory(FixtureDir).ToList();

        rows.Should().NotBeEmpty();
        rows.Select(r => (r.SourceFile, r.Game)).Should().OnlyHaveUniqueItems(
            "StopGameAfter=true after a yield must cut the rest of the game");
    }

    // -----------------------------------------------------------------------
    //  Illegal-play fixture — one game, the illegal marker between two legal
    //  moves. Mirrors ConvertXgToJson_Lib's producer fixture; the file is
    //  round-tripped through JSON so it reaches the iterator via a real read.
    // -----------------------------------------------------------------------

    private static XgFile BuildFileWithIllegalPlay() =>
        new XgFile
        {
            Records =
            {
                new MatchHeaderRecord { EntryType = RecordType.HeaderMatch, MatchLength = 7, Player1 = "P1", Player2 = "P2" },
                new GameHeaderRecord
                {
                    EntryType = RecordType.HeaderGame,
                    InitialPosition = new PositionEngine { Points = StandardOpening() },
                },
                MakeMove([23, 22, -1, -1, -1, -1, -1, -1], dice: [3, 1]),
                MakeMove([-100, 10, 10, 7, 6, 7, 6, 7],    dice: [5, 2]),  // illegal-play marker
                MakeMove([23, 22, -1, -1, -1, -1, -1, -1], dice: [3, 1]),
            },
        };

    private static MoveRecord MakeMove(sbyte[] moves, int[] dice)
    {
        var pos = new PositionEngine { Points = OneCheckerOn24() };
        return new MoveRecord
        {
            EntryType = RecordType.Move,
            InitialPosition = pos,
            FinalPosition = pos,            // unused on the skip path
            ActivePlayer = 1,
            Dice = dice,
            CubeValue = 0,
            MoveError = -1000.0,            // unanalysed-error sentinel
            Analysis = new BestMoveAnalysis
            {
                MoveCount = 1,
                Evals = [new EvalResult { Equity = 0.0f }],
                Moves = [moves],
                EvalLevels = [new EvalLevel { Level = 1 }],
                PositionsPlayed = [pos],
            },
            RolloutIndices = new int[32].Select(_ => -1).ToArray(),
        };
    }

    private static sbyte[] OneCheckerOn24()
    {
        var pts = new sbyte[26];
        pts[24] = 1;
        return pts;
    }

    private static sbyte[] StandardOpening()
    {
        var pts = new sbyte[26];
        pts[6]  = -5; pts[8]  = -3; pts[13] =  5; pts[24] = -2;
        pts[19] =  5; pts[17] =  3; pts[12] = -5; pts[1]  =  2;
        return pts;
    }

    // -----------------------------------------------------------------------
    //  Spy filters used by the early-exit pipeline tests
    // -----------------------------------------------------------------------

    private sealed class SkipAllMatchesFilter : IDecisionFilter, IMatchFilter
    {
        public bool Matches(IDecisionFilterData data) => true;
        public bool ShouldSkipMatch(XgMatchInfo match) => true;
        public bool ShouldSkipGame(XgGameInfo game) => false;
    }

    private sealed class SkipAllGamesFilter : IDecisionFilter, IMatchFilter
    {
        public bool Matches(IDecisionFilterData data) => true;
        public bool ShouldSkipMatch(XgMatchInfo match) => false;
        public bool ShouldSkipGame(XgGameInfo game) => true;
    }

    private sealed class AdvanceMatchOnAnyRowFilter : IDecisionFilter
    {
        public bool Matches(IDecisionFilterData data) => true;
        public bool ShouldAdvanceMatch(IDecisionFilterData data) => true;
    }

    private sealed class AdvanceGameOnAnyRowFilter : IDecisionFilter
    {
        public bool Matches(IDecisionFilterData data) => true;
        public bool ShouldAdvanceGame(IDecisionFilterData data) => true;
    }

    // -----------------------------------------------------------------------
    //  Test double — captures structured log entries for assertion
    // -----------------------------------------------------------------------

    private sealed class ListLogger<T> : ILogger<T>
    {
        public List<LogEntry> Entries { get; } = [];

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(
            LogLevel logLevel, EventId eventId, TState state,
            Exception? exception, Func<TState, Exception?, string> formatter)
        {
            Entries.Add(new LogEntry(logLevel, formatter(state, exception), exception));
        }
    }

    private sealed record LogEntry(LogLevel Level, string Message, Exception? Exception);
}
