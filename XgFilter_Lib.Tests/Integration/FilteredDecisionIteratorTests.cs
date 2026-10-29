using BgDataTypes_Lib;
using BgDataTypes_Lib.TestSupport;
using ConvertXgToJson_Lib;
using ConvertXgToJson_Lib.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using XgFilter_Lib.Classification;
using XgFilter_Lib.Enums;
using XgFilter_Lib.Filtering;
using XgFilter_Lib.Tests.Helpers;

namespace XgFilter_Lib.Tests.Integration;

/// <summary>
/// The <see cref="FilteredDecisionIterator"/> entry points end to end: the
/// directory and stream walks, their parity, the filter pipeline's votes and
/// the malformed-source path. Most tests synthesize their sources
/// (<see cref="SyntheticSources"/>), written as XG's binary container and read
/// back through the real reader, and gate. Three read the umbrella's
/// <c>TestData/xg</c> in place, which may be empty, and check invariants that
/// hold over whatever it holds; two read <c>TestData/FixtureFiles</c> in place
/// (<see cref="TestPaths"/>) and carry <c>RequiresFixtureFiles</c>.
/// Every iterator here runs under the default ranking; the ranking in force is
/// pinned on synthesized files by <see cref="FilteredDecisionIteratorRankingTests"/>.
/// </summary>
public class FilteredDecisionIteratorTests
{
    private static readonly ILogger<FilteredDecisionIterator> NullLogger =
        NullLogger<FilteredDecisionIterator>.Instance;

    private static FilteredDecisionIterator NewIterator(DecisionFilterSet filters) =>
        new FilteredDecisionIterator(filters, PlayRanking.Equity, NullLogger);

    /// <summary>
    /// Two matches, one with halheinrich and one without, and a position:
    /// more than one source, more than one game per match, both XG formats.
    /// </summary>
    private static readonly (string Name, XgFile File)[] Sources =
    [
        ("halheinrich.xg", SyntheticSources.TwoGameMatch("halheinrich", "Opponent")),
        ("others.xg", SyntheticSources.TwoGameMatch("P3", "P4")),
        ("position.xgp", SyntheticSources.Position()),
    ];

    /// <summary>
    /// Writes <paramref name="sources"/> as XG's binary container into a fresh
    /// temp directory and returns the path; the caller deletes it.
    /// </summary>
    private static string Stage(IEnumerable<(string Name, XgFile File)> sources)
    {
        var tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(tempDir);
        foreach (var (name, file) in sources)
            File.WriteAllBytes(Path.Combine(tempDir, name), SyntheticSources.XgBytes(file));
        return tempDir;
    }

    /// <summary>
    /// Runs <paramref name="test"/> over <see cref="Sources"/> staged into a
    /// temp directory, deleting it afterwards.
    /// </summary>
    private static void WithStagedSources(Action<string> test)
    {
        var tempDir = Stage(Sources);
        try { test(tempDir); }
        finally { Directory.Delete(tempDir, recursive: true); }
    }

    [Fact]
    [Trait("Category", "RequiresFixtureFiles")]
    public void IterateXgDirectory_FilterByPlayer_ReturnsOnlyMatchingRows()
    {
        var iterator = NewIterator(
            new DecisionFilterSet().Add(new PlayerErrorFilter(["halheinrich"])));

        var rows = iterator.IterateXgDirectory(TestPaths.FixtureFilesDir).ToList();

        rows.Should().NotBeEmpty("expected at least one decision by halheinrich in the test files");
        rows.Should().OnlyContain(r => MadeBy(r, "halheinrich"));
        rows.Should().Contain(r => IsNamed(r.Taker, "halheinrich"),
            "the corpus holds doubles halheinrich answered, which his name admits (SPEC-filtering §8)");
    }

    /// <summary>
    /// Whether <paramref name="player"/> made a decision on <paramref name="data"/>
    /// — on roll, or as the taker of a double offered (SPEC-filtering §8) —
    /// which is what a name filter admits.
    /// </summary>
    private static bool MadeBy(IDecisionFilterData data, string player) =>
        IsNamed(data.Player, player) || IsNamed(data.Taker, player);

    private static bool IsNamed(string? name, string player) =>
        string.Equals(name, player, StringComparison.OrdinalIgnoreCase);

    [Fact]
    public void IterateXgDirectory_FilterByRace_ListsDecisions()
    {
        var iterator = NewIterator(
            new DecisionFilterSet().Add(new ContactTypeFilter([ContactType.Race])));

        var rows = iterator.IterateXgDirectory(TestPaths.XgDir).ToList();

        rows.Should().OnlyContain(r => new RaceClassifier().Matches(r.Board));
    }

    [Fact]
    public void IterateXgDirectory_FilterByContact_ListsDecisions()
    {
        var iterator = NewIterator(
            new DecisionFilterSet().Add(new ContactTypeFilter([ContactType.Contact])));

        var rows = iterator.IterateXgDirectory(TestPaths.XgDir).ToList();

        rows.Should().OnlyContain(r => new ContactClassifier().Matches(r.Board));
    }

    [Fact]
    public void IterateXgDirectory_RaceAndContactAreComplementary()
    {
        var allIterator = NewIterator(new DecisionFilterSet());
        var raceIterator = NewIterator(
            new DecisionFilterSet().Add(new ContactTypeFilter([ContactType.Race])));
        var contactIterator = NewIterator(
            new DecisionFilterSet().Add(new ContactTypeFilter([ContactType.Contact])));

        var all = allIterator.IterateXgDirectory(TestPaths.XgDir).ToList();
        var race = raceIterator.IterateXgDirectory(TestPaths.XgDir).ToList();
        var contact = contactIterator.IterateXgDirectory(TestPaths.XgDir).ToList();

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
                new DecisionFilterSet().Add(new PlayerErrorFilter(["halheinrich"])));

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
    public void IterateXgDirectoryDiagrams_FilterByPlayer_ReturnsOnlyMatchingDiagrams() => WithStagedSources(dir =>
    {
        var iterator = NewIterator(
            new DecisionFilterSet().Add(new PlayerErrorFilter(["halheinrich"])));

        var diagrams = iterator.IterateXgDirectoryDiagrams(dir).ToList();

        diagrams.Should().NotBeEmpty(
            "expected at least one decision by halheinrich in the staged sources");
        diagrams.Should().OnlyContain(d => MadeBy(d.ViewFor(PlayRanking.Equity), "halheinrich"));
    });

    [Fact]
    public void IterateXgDirectoryDiagrams_AtLeastOneDecisionHasPlays() => WithStagedSources(dir =>
    {
        // The diagram form's whole purpose is the per-candidate Plays list.
        // No filter — take every staged source and assert that some
        // checker-play decision yields its candidates. A cube decision has no
        // candidates at all — the other kind.
        var iterator = NewIterator(new DecisionFilterSet());

        var diagrams = iterator.IterateXgDirectoryDiagrams(dir).ToList();

        diagrams.Should().NotBeEmpty();
        diagrams.Should().Contain(d => d is CheckerPlayDecision && ((CheckerPlayDecision)d).Decision.Plays.Count > 0,
            "diagram form must populate Plays for checker-play decisions");
    });

    [Fact]
    public void IterateXgDirectoryDiagrams_RowVariant_AndDiagramVariant_AgreeOnPassingDecisions() => WithStagedSources(dir =>
    {
        // Same sources, same filter set: a row and its record's view are one
        // decision under one ranking — the pass/fail outcome must be
        // identical decision-for-decision. Compare on the identifier both
        // shapes carry.
        var iterator = NewIterator(
            new DecisionFilterSet().Add(new PlayerErrorFilter(["halheinrich"])));

        var rowKeys = iterator.IterateXgDirectory(dir)
            .Select(r => r.Id)
            .ToList();

        var diagramKeys = iterator.IterateXgDirectoryDiagrams(dir)
            .Select(d => d.Id)
            .ToList();

        rowKeys.Should().NotBeEmpty("the filter must pass some decisions for the agreement to mean anything");
        diagramKeys.Should().Equal(rowKeys,
            "row and diagram variants must yield the same decisions in the same order for the same filter set");
    });

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
        // Stage a temp dir containing only .xgp sources — if the iterator
        // returns rows from there, .xgp enumeration is wired.
        var xgpSources = Sources.Where(s => Path.GetExtension(s.Name) == ".xgp").ToList();
        var tempDir = Stage(xgpSources);

        try
        {
            // SourceFile carries the extension (e.g. "foo.xgp"), matching the
            // producer's post-Arc-2 contract — see XgDecisionIterator's
            // sourceFile-with-extension requirement for DecisionId stamping.
            var xgpBaseNames = xgpSources.Select(s => s.Name).ToHashSet();

            var iterator = NewIterator(new DecisionFilterSet());
            var rows = iterator.IterateXgDirectory(tempDir).ToList();

            rows.Should().NotBeEmpty(
                "an XG-format directory walk must include .xgp position files");
            rows.Should().OnlyContain(r => xgpBaseNames.Contains(r.SourceFile),
                "every row must trace back to one of the .xgp sources placed in the temp dir");
        }
        finally
        {
            Directory.Delete(tempDir, recursive: true);
        }
    }

    [Fact]
    public void IterateXgDirectoryDiagrams_IncludesXgpPositionFiles()
    {
        var xgpSources = Sources.Where(s => Path.GetExtension(s.Name) == ".xgp").ToList();
        var tempDir = Stage(xgpSources);

        try
        {
            // SourceFile carries the extension (e.g. "foo.xgp"), matching the
            // producer's post-Arc-2 contract — see XgDecisionIterator's
            // sourceFile-with-extension requirement for DecisionId stamping.
            var xgpBaseNames = xgpSources.Select(s => s.Name).ToHashSet();

            var iterator = NewIterator(new DecisionFilterSet());
            var diagrams = iterator.IterateXgDirectoryDiagrams(tempDir).ToList();

            diagrams.Should().NotBeEmpty(
                "the diagram-shape iterator must mirror the row-shape iterator's .xgp parity");
            diagrams.Should().OnlyContain(
                d => xgpBaseNames.Contains(d.SourceFile));
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
    /// <see cref="Sources"/> as in-memory <see cref="XgFileStream"/>s, fresh
    /// per call, so the streams outlive the lazy enumeration — the same bytes
    /// <see cref="Stage"/> writes to disk, in the order a directory walk reads
    /// them (all <c>*.xg</c>, then all <c>*.xgp</c>).
    /// </summary>
    private static List<XgFileStream> LoadStreams() =>
        [.. Sources.Select(s => SyntheticSources.XgEntry(s.Name, s.File))];

    [Fact]
    public void IterateXgStreams_MatchesDirectoryWalk_RowForRow() => WithStagedSources(dir =>
    {
        // The stream entry must yield exactly what the directory walk does
        // for the same sources and filter set. Read each source both ways and
        // compare on identity fields both shapes carry.
        var iterator = NewIterator(new DecisionFilterSet());

        var fromDir = iterator.IterateXgDirectory(dir)
            .Select(r => (r.Id, r.Player))
            .ToList();

        var fromStreams = iterator.IterateXgStreams(LoadStreams())
            .Select(r => (r.Id, r.Player))
            .ToList();

        fromStreams.Should().NotBeEmpty(
            "the sources must yield at least one decision");
        fromStreams.Should().Equal(fromDir,
            "stream iteration must reproduce the directory walk decision-for-decision");
    });

    [Fact]
    public void IterateXgStreamDiagrams_MatchesDirectoryWalk_RowForRow() => WithStagedSources(dir =>
    {
        var iterator = NewIterator(new DecisionFilterSet());

        var fromDir = iterator.IterateXgDirectoryDiagrams(dir)
            .Select(d => (d.Id, d.Player))
            .ToList();

        var fromStreams = iterator.IterateXgStreamDiagrams(LoadStreams())
            .Select(d => (d.Id, d.Player))
            .ToList();

        fromStreams.Should().NotBeEmpty();
        fromStreams.Should().Equal(fromDir,
            "stream diagram iteration must reproduce the directory walk decision-for-decision");
    });

    [Fact]
    public void IterateXgStreams_SourceFileCarriesExtension()
    {
        // The whole point of the stream API is that the caller-supplied name
        // (with extension) flows through untouched — DecisionId stamping
        // depends on Path.GetExtension(SourceFile) downstream.
        var iterator = NewIterator(new DecisionFilterSet());

        var rows = iterator.IterateXgStreams(LoadStreams()).ToList();

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
            new DecisionFilterSet().Add(new PlayerErrorFilter(["halheinrich"])));

        var rows = iterator.IterateXgStreams(LoadStreams()).ToList();

        rows.Should().NotBeEmpty(
            "expected at least one decision by halheinrich in the sources");
        rows.Should().OnlyContain(r => MadeBy(r, "halheinrich"));
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
        // .xg stream with readable ones and assert survival + a warning.
        var spyLogger = new RecordingLogger<FilteredDecisionIterator>();
        var iterator = new FilteredDecisionIterator(new DecisionFilterSet(), PlayRanking.Equity, spyLogger);

        var streams = LoadStreams();
        streams.Add(SyntheticSources.MalformedEntry("malformed.xg"));

        var rows = iterator.IterateXgStreams(streams).ToList();

        rows.Should().NotBeEmpty("the valid sources must still yield rows");

        // The forwarded producer logger may also emit warnings from the valid
        // sources; the malformed-stream skip is the lone warning that
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
        var act = () => new FilteredDecisionIterator(null!, PlayRanking.Equity, NullLogger);
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void Constructor_NullLogger_Throws()
    {
        var act = () => new FilteredDecisionIterator(new DecisionFilterSet(), PlayRanking.Equity, null!);
        act.Should().Throw<ArgumentNullException>();
    }

    // -----------------------------------------------------------------------
    //  Malformed-file resilience — folds in T3-5
    // -----------------------------------------------------------------------

    [Fact]
    public void IterateXgDirectory_MalformedFile_IsSkippedAndLogged_IterationContinues()
    {
        // Mix one malformed .xg file with readable ones. The iterator must
        // skip the malformed file (logging it as a warning) and yield rows
        // from the surviving ones, never throwing. This pins the contract
        // Task 1a establishes: log + continue, never swallow + continue.
        var tempDir = Stage(Sources);

        try
        {
            // Drop a zero-byte garbage .xg file alongside.
            var badPath = Path.Combine(tempDir, "malformed.xg");
            File.WriteAllBytes(badPath, []);

            var spyLogger = new RecordingLogger<FilteredDecisionIterator>();
            var iterator = new FilteredDecisionIterator(new DecisionFilterSet(), PlayRanking.Equity, spyLogger);

            var rows = iterator.IterateXgDirectory(tempDir).ToList();

            rows.Should().NotBeEmpty("the surviving sources should still yield rows");

            // The forwarded producer logger may also emit warnings from the
            // surviving sources; the malformed-file skip is the lone
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
            // JSON round-trip (XgFileReader.ToJson) so it flows through the
            // real file-read path rather than bypassing it.
            var json = XgFileReader.ToJson(BuildFileWithIllegalPlay());
            File.WriteAllText(Path.Combine(tempDir, "illegal.json"), json);

            var spyLogger = new RecordingLogger<FilteredDecisionIterator>();
            var iterator = new FilteredDecisionIterator(new DecisionFilterSet(), PlayRanking.Equity, spyLogger);

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
    [Trait("Category", "RequiresFixtureFiles")]
    public void IterateXgDirectory_RealFixtureWithIllegalPlay_ProducerWarningSurfacesThroughSuppliedLogger()
    {
        // Companion to the synthetic test above, on the primary production
        // path: the real binary .xg read rather than the JSON detour. The
        // FixtureFiles corpus includes a tournament file that carries XG's
        // illegal-play marker, so walking it must surface the producer's
        // "Illegal play" warning through the iterator's supplied logger.
        const string knownCarrier =
            "Avi Cohen (6.86) - Max Stockslager (10.55) 2023-02-09_18122.xg";

        var spyLogger = new RecordingLogger<FilteredDecisionIterator>();
        var iterator = new FilteredDecisionIterator(new DecisionFilterSet(), PlayRanking.Equity, spyLogger);

        _ = iterator.IterateXgDirectory(TestPaths.FixtureFilesDir).ToList();

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
    public void ShouldSkipMatch_VotedByFilter_IteratorSkipsEveryFile() => WithStagedSources(dir =>
    {
        // Spy filter votes skip-every-match. The iterator must consult
        // ShouldSkipMatch before walking any file's rows; if it doesn't,
        // we'll see rows in the output. Zero rows is the contract.
        var iterator = new FilteredDecisionIterator(
            new DecisionFilterSet().Add(new SkipAllMatchesFilter()), PlayRanking.Equity, NullLogger);

        NewIterator(new DecisionFilterSet()).IterateXgDirectory(dir).Should().NotBeEmpty(
            "the sources must hold rows for skipping them to be observable");
        iterator.IterateXgDirectory(dir).Should().BeEmpty(
            "ShouldSkipMatch=true must short-circuit the file before any row is yielded");
    });

    [Fact]
    public void ShouldSkipGame_VotedByFilter_IteratorSkipsEveryGame() => WithStagedSources(dir =>
    {
        var iterator = new FilteredDecisionIterator(
            new DecisionFilterSet().Add(new SkipAllGamesFilter()), PlayRanking.Equity, NullLogger);

        NewIterator(new DecisionFilterSet()).IterateXgDirectory(dir).Should().NotBeEmpty(
            "the sources must hold rows for skipping them to be observable");
        iterator.IterateXgDirectory(dir).Should().BeEmpty(
            "ShouldSkipGame=true on every game must keep any row from being yielded");
    });

    [Fact]
    public void ShouldAdvanceMatch_VotedByFilter_AtMostOneRowPerSourceFile() => WithStagedSources(dir =>
    {
        // Spy filter votes advance-match on the first matching row of any
        // file. If the iterator honors StopMatchAfter, exactly one row
        // per file should reach the consumer. Pin via SourceFile-uniqueness.
        var iterator = new FilteredDecisionIterator(
            new DecisionFilterSet().Add(new AdvanceMatchOnAnyRowFilter()), PlayRanking.Equity, NullLogger);

        var rows = iterator.IterateXgDirectory(dir).ToList();

        rows.Should().NotBeEmpty("the iterator must yield each file's first decision");
        rows.Should().HaveCountGreaterThan(1,
            "the sources are several files; iteration must reach more than one");
        rows.Select(r => r.SourceFile).Should().OnlyHaveUniqueItems(
            "StopMatchAfter=true after a yield must cut the rest of the file");
    });

    [Fact]
    public void ShouldAdvanceGame_VotedByFilter_AtMostOneRowPerGame() => WithStagedSources(dir =>
    {
        var iterator = new FilteredDecisionIterator(
            new DecisionFilterSet().Add(new AdvanceGameOnAnyRowFilter()), PlayRanking.Equity, NullLogger);

        var rows = iterator.IterateXgDirectory(dir).ToList();

        rows.Should().NotBeEmpty();
        rows.Select(r => (r.SourceFile, r.Game)).Should().OnlyHaveUniqueItems(
            "StopGameAfter=true after a yield must cut the rest of the game");
    });

    // -----------------------------------------------------------------------
    //  Illegal-play fixture — one game, the illegal marker between two legal
    //  moves. Mirrors ConvertXgToJson_Lib's producer fixture; the file is
    //  round-tripped through JSON so it reaches the iterator via a real read.
    // -----------------------------------------------------------------------

    private static XgFile BuildFileWithIllegalPlay()
    {
        var builder = XgFileBuilder.ForMatch(7, "P1", "P2");
        builder.AddGame()
            .Play(XgPlayer.Player1, new DiceRoll(3, 1), MakeFivePoint)
            .IllegalPlay(XgPlayer.Player2, new DiceRoll(5, 2))
            .Play(XgPlayer.Player2, new DiceRoll(3, 1), MakeFivePoint);
        return builder.Build();
    }

    /// <summary>8/5 6/5 — the classic 3-1, legal for either side from the opening.</summary>
    private static Play MakeFivePoint => [new(8, 5), new(6, 5)];

    // -----------------------------------------------------------------------
    //  Spy filters used by the early-exit pipeline tests
    // -----------------------------------------------------------------------

    private sealed class SkipAllMatchesFilter : IDecisionFilter, IMatchFilter
    {
        public bool Matches(IDecisionFilterData data) => true;
        public bool ShouldSkipMatch(IMatchInfo match) => true;
        public bool ShouldSkipGame(IGameInfo game) => false;
    }

    private sealed class SkipAllGamesFilter : IDecisionFilter, IMatchFilter
    {
        public bool Matches(IDecisionFilterData data) => true;
        public bool ShouldSkipMatch(IMatchInfo match) => false;
        public bool ShouldSkipGame(IGameInfo game) => true;
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
}
