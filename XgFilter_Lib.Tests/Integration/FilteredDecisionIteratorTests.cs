using BgDataTypes_Lib;
using ConvertXgToJson_Lib;
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
            new DecisionFilterSet().Add(new PositionTypeFilter([PositionType.Race])));

        var rows = iterator.IterateXgDirectory(XgDir).ToList();

        rows.Should().OnlyContain(r => r.Board.Count == 26);
    }

    [Fact]
    public void IterateXgDirectory_FilterByContact_ListsDecisions()
    {
        var iterator = NewIterator(
            new DecisionFilterSet().Add(new PositionTypeFilter([PositionType.Contact])));

        var rows = iterator.IterateXgDirectory(XgDir).ToList();

        rows.Should().OnlyContain(r => r.Board.Count == 26);
    }

    [Fact]
    public void IterateXgDirectory_RaceAndContactAreComplementary()
    {
        var allIterator = NewIterator(new DecisionFilterSet());
        var raceIterator = NewIterator(
            new DecisionFilterSet().Add(new PositionTypeFilter([PositionType.Race])));
        var contactIterator = NewIterator(
            new DecisionFilterSet().Add(new PositionTypeFilter([PositionType.Contact])));

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
            spyLogger.Entries.Should().ContainSingle(
                "the one malformed file should produce one warning")
                .Which.Level.Should().Be(LogLevel.Warning);
            spyLogger.Entries[0].Message.Should().Contain("malformed.xg");
            spyLogger.Entries[0].Exception.Should().NotBeNull(
                "the warning must carry the original exception, not a stringified message");
        }
        finally
        {
            Directory.Delete(tempDir, recursive: true);
        }
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
