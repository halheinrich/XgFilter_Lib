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
