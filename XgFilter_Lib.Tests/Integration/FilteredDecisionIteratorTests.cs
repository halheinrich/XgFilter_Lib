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
        var filters = new DecisionFilterSet()
            .Add(new PlayerFilter(["halheinrich"]));

        var rows = FilteredDecisionIterator.IterateXgDirectory(XgDir, filters).ToList();

        rows.Should().NotBeEmpty("expected at least one decision by halheinrich in the test files");
        rows.Should().OnlyContain(r => r.Player.Equals("halheinrich", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void IterateXgDirectory_FilterByPlayer_ListsDecisions()
    {
        var filters = new DecisionFilterSet()
            .Add(new PlayerFilter(["halheinrich"]));

        var rows = FilteredDecisionIterator.IterateXgDirectory(XgDir, filters).ToList();

        // Output all columns matching DecisionRow field order
        Console.WriteLine($"{"Xgid",-52} {"Error",8} {"MatchScore",-12} {"MatchLength",11} {"Player",-15} {"Match",-45} {"Game",4} {"MoveNum",7} {"Roll",4} {"AnalysisDepth",-30} {"Equity",8}");
        Console.WriteLine(new string('-', 210));
        foreach (var row in rows)
        {
            Console.WriteLine($"{row.Xgid,-52} {row.Error,8:F4} {row.MatchScore,-12} {row.MatchLength,11} {row.Player,-15} {row.Match,-45} {row.Game,4} {row.MoveNum,7} {row.Roll,4} {row.AnalysisDepth,-30} {row.Equity,8:F4}");
        }
        Console.WriteLine(new string('-', 210));
        Console.WriteLine($"Total: {rows.Count} decisions");

        rows.Should().NotBeEmpty();
    }

    [Fact]
    public void IterateXgDirectory_FilterByMatchScoreCubeOnlyNonZeroError_ListsDecisions()
    {
        var filters = new DecisionFilterSet()
            .Add(new MatchScoreFilter(["11a11a"]))
            .Add(new DecisionTypeFilter(DecisionTypeOption.CubeOnly))
            .Add(new ErrorRangeFilter(min: double.Epsilon));

        var rows = FilteredDecisionIterator.IterateXgDirectory(XgDir, filters).ToList();

        var selector = new ColumnSelector();

        Console.WriteLine(selector.Header);
        Console.WriteLine(new string('-', selector.Header.Length));
        foreach (var row in rows.Take(32))
            Console.WriteLine(selector.Serialize(row));
        Console.WriteLine(new string('-', selector.Header.Length));
        Console.WriteLine($"Showing {Math.Min(32, rows.Count)} of {rows.Count} decisions");
    }

    [Fact]
    public void IterateXgDirectory_FilterByMatchScoreAndCubeOnly_ListsDecisions()
    {
        var filters = new DecisionFilterSet()
            .Add(new MatchScoreFilter(["11a11a"]))
            .Add(new DecisionTypeFilter(DecisionTypeOption.CubeOnly));

        var rows = FilteredDecisionIterator.IterateXgDirectory(XgDir, filters).ToList();

        var selector = new ColumnSelector();

        Console.WriteLine(selector.Header);
        Console.WriteLine(new string('-', selector.Header.Length));
        foreach (var row in rows.Take(32))
            Console.WriteLine(selector.Serialize(row));
        Console.WriteLine(new string('-', selector.Header.Length));
        Console.WriteLine($"Showing {Math.Min(32, rows.Count)} of {rows.Count} decisions");
    }

    [Fact]
    public void IterateXgDirectory_FilterByMatchScore_ListsDecisions()
    {
        var filters = new DecisionFilterSet()
            .Add(new MatchScoreFilter(["11a11a"]));

        var rows = FilteredDecisionIterator.IterateXgDirectory(XgDir, filters).ToList();

        var selector = new ColumnSelector();

        Console.WriteLine(selector.Header);
        Console.WriteLine(new string('-', selector.Header.Length));
        foreach (var row in rows.Take(32))
            Console.WriteLine(selector.Serialize(row));
        Console.WriteLine(new string('-', selector.Header.Length));
        Console.WriteLine($"Showing {Math.Min(32, rows.Count)} of {rows.Count} decisions");
    }

    [Fact]
    public void IterateXgDirectory_FilterByPlayer_CustomColumns()
    {
        var filters = new DecisionFilterSet()
            .Add(new PlayerFilter(["halheinrich"]));

        var rows = FilteredDecisionIterator.IterateXgDirectory(XgDir, filters).ToList();

        var selector = new ColumnSelector(["Player", "Match", "Game", "MoveNum", "Roll", "Error", "Equity"]);

        Console.WriteLine(selector.Header);
        Console.WriteLine(new string('-', selector.Header.Length));
        foreach (var row in rows.Take(32))
            Console.WriteLine(selector.Serialize(row));
        Console.WriteLine(new string('-', selector.Header.Length));
        Console.WriteLine($"Showing {Math.Min(32, rows.Count)} of {rows.Count} decisions");

        rows.Should().NotBeEmpty();
    }

    [Fact]
    public void IterateXgDirectory_FilterByRace_ListsDecisions()
    {
        var filters = new DecisionFilterSet()
            .Add(new PositionTypeFilter([PositionType.Race]));

        var rows = FilteredDecisionIterator.IterateXgDirectory(XgDir, filters).ToList();

        var selector = new ColumnSelector();

        Console.WriteLine(selector.Header);
        Console.WriteLine(new string('-', selector.Header.Length));
        foreach (var row in rows.Take(32))
            Console.WriteLine(selector.Serialize(row));
        Console.WriteLine(new string('-', selector.Header.Length));
        Console.WriteLine($"Showing {Math.Min(32, rows.Count)} of {rows.Count} decisions");

        rows.Should().OnlyContain(r => r.Board.Count == 26);
    }

    [Fact]
    public void IterateXgDirectory_FilterByContact_ListsDecisions()
    {
        var filters = new DecisionFilterSet()
            .Add(new PositionTypeFilter([PositionType.Contact]));

        var rows = FilteredDecisionIterator.IterateXgDirectory(XgDir, filters).ToList();

        var selector = new ColumnSelector();

        Console.WriteLine(selector.Header);
        Console.WriteLine(new string('-', selector.Header.Length));
        foreach (var row in rows.Take(32))
            Console.WriteLine(selector.Serialize(row));
        Console.WriteLine(new string('-', selector.Header.Length));
        Console.WriteLine($"Showing {Math.Min(32, rows.Count)} of {rows.Count} decisions");

        rows.Should().OnlyContain(r => r.Board.Count == 26);
    }

    [Fact]
    public void IterateXgDirectory_RaceAndContactAreComplementary()
    {
        var allFilters = new DecisionFilterSet();
        var raceFilters = new DecisionFilterSet().Add(new PositionTypeFilter([PositionType.Race]));
        var contactFilters = new DecisionFilterSet().Add(new PositionTypeFilter([PositionType.Contact]));

        var all = FilteredDecisionIterator.IterateXgDirectory(XgDir, allFilters).ToList();
        var race = FilteredDecisionIterator.IterateXgDirectory(XgDir, raceFilters).ToList();
        var contact = FilteredDecisionIterator.IterateXgDirectory(XgDir, contactFilters).ToList();

        (race.Count + contact.Count).Should().Be(all.Count,
            "Race and Contact should be mutually exclusive and exhaustive");

        Console.WriteLine($"Total: {all.Count}  Race: {race.Count}  Contact: {contact.Count}");
    }

    [Fact]
    public void IterateXgDirectory_WhenNoFilesPresent_ReturnsEmpty()
    {
        var emptyDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(emptyDir);

        try
        {
            var filters = new DecisionFilterSet()
                .Add(new PlayerFilter(["halheinrich"]));

            var rows = FilteredDecisionIterator.IterateXgDirectory(emptyDir, filters).ToList();

            rows.Should().BeEmpty();
        }
        finally
        {
            Directory.Delete(emptyDir);
        }
    }
}