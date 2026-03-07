using XgFilter_Lib.Filtering;

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