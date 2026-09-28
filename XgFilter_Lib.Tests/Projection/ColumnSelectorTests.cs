using System.Globalization;
using BgDataTypes_Lib;
using BgDataTypes_Lib.TestSupport;
using XgFilter_Lib.Enums;
using XgFilter_Lib.Projection;
using XgFilter_Lib.Tests.Helpers;

namespace XgFilter_Lib.Tests.Projection;

public class ColumnSelectorTests
{
    // -----------------------------------------------------------------------
    //  Rows — each the producer's projection of a TestRecords record
    // -----------------------------------------------------------------------

    /// <summary>The opening 3-1 by <paramref name="player"/>, who erred by exactly <paramref name="error"/>.</summary>
    private static DecisionRow Erred(string player, double error) =>
        TestRecords.Row(TestRecords.CheckerPlay(
            decision: TestRecords.CheckerPlayData(
                plays:
                [
                    TestRecords.Candidate(play: [new(8, 5), new(6, 5)], equity: 0.0),
                    TestRecords.Candidate(play: [new(13, 10), new(6, 5)], equity: -error),
                ],
                userPlayIndex: 1),
            descriptive: TestRecords.Descriptive(onRollName: player)));

    private static DecisionRow PlayedBy(string player) =>
        TestRecords.Row(TestRecords.CheckerPlay(descriptive: TestRecords.Descriptive(onRollName: player)));

    /// <summary>
    /// Rows covering every way a column can be empty: a cube decision (no
    /// roll), a money session (no match length), a standalone position (no
    /// game, no move number), no move recorded (no error), a move depth first
    /// does not score (no error), and no player name recorded.
    /// </summary>
    private static IEnumerable<DecisionRow> RowsOfEveryShape()
    {
        yield return TestRecords.Row();
        yield return Erred("Alice", 0.05);
        yield return TestRecords.Row(TestRecords.Cube());
        yield return TestRecords.Row(TestRecords.CheckerPlay(
            position: TestRecords.Position(session: TestRecords.MoneySession(isJacoby: false))));
        yield return TestRecords.Row(TestRecords.CheckerPlay(id: new XgpDecisionId("position.xgp")));
        yield return TestRecords.Row(TestRecords.CheckerPlay(
            decision: TestRecords.CheckerPlayData(userPlayIndex: null)));
        yield return TestRecords.Row(RankingSplit.Played(RankingSplit.ShallowHigh), PlayRanking.DepthFirst);
        yield return TestRecords.Row(TestRecords.CheckerPlay(
            descriptive: TestRecords.Descriptive(onRollName: null)));
    }

    // -----------------------------------------------------------------------
    //  Default constructor — all columns in declaration order
    // -----------------------------------------------------------------------

    [Fact]
    public void DefaultConstructor_SelectedColumns_MatchesAllColumns()
    {
        var selector = new ColumnSelector();

        selector.SelectedColumns.Should().Equal(ColumnSelector.AllColumns);
    }

    [Fact]
    public void DefaultConstructor_HeaderContainsEveryColumnLabel()
    {
        var selector = new ColumnSelector();
        var header = selector.Header;

        foreach (var column in ColumnSelector.AllColumns)
            header.Should().Contain(column.ToLabel());
    }

    [Fact]
    public void DefaultConstructor_SerializeContainsAllValues()
    {
        var selector = new ColumnSelector();

        var line = selector.Serialize(Erred("Alice", 0.05));

        line.Should().Contain("Alice");
        line.Should().Contain("0.05");
        line.Should().Contain("31");
    }

    // -----------------------------------------------------------------------
    //  Explicit column selection
    // -----------------------------------------------------------------------

    [Fact]
    public void ExplicitColumns_HeaderMatchesSelection()
    {
        var selector = new ColumnSelector([Column.Player, Column.Error]);

        selector.Header.Should().Be("Player,Error");
    }

    [Fact]
    public void ExplicitColumns_SerializeOnlyIncludesSelectedColumns()
    {
        var selector = new ColumnSelector([Column.Player, Column.Error]);

        var line = selector.Serialize(Erred("Alice", 0.05));

        line.Should().Be("Alice,0.05"); // Roll (31) not selected
    }

    [Fact]
    public void ExplicitColumns_OrderIsPreserved()
    {
        var selector = new ColumnSelector([Column.Error, Column.Player]);

        selector.Header.Should().Be("Error,Player");
    }

    [Fact]
    public void ExplicitColumns_SingleColumn_HeaderAndSerializeWork()
    {
        var selector = new ColumnSelector([Column.Player]);

        selector.Header.Should().Be("Player");
        selector.Serialize(PlayedBy("Bob")).Should().Be("Bob");
    }

    [Fact]
    public void ExplicitColumns_Empty_HeaderAndSerializeAreEmpty()
    {
        var selector = new ColumnSelector([]);

        selector.Header.Should().BeEmpty();
        selector.Serialize(PlayedBy("Alice")).Should().BeEmpty();
    }

    // -----------------------------------------------------------------------
    //  Cells — written as the row's own CSV writes them
    // -----------------------------------------------------------------------

    [Fact]
    public void EveryColumn_WritesTheCellTheRowsOwnCsvWrites()
    {
        // Each column is one of DecisionRow's CSV columns, so its cell is the
        // row's own (DecisionRow.ToCsvLine): an empty cell for a fact that
        // does not apply, never 0, and the same number formatting. None of
        // these rows holds a comma in a cell, so the row's line splits cleanly.
        var rowHeader = DecisionRow.CsvHeader.Split(',');
        var selector = new ColumnSelector();

        foreach (var row in RowsOfEveryShape())
        {
            var rowCells = row.ToCsvLine().Split(',');
            var cells = selector.Serialize(row).Split(',');

            for (int i = 0; i < selector.SelectedColumns.Count; i++)
            {
                var column = selector.SelectedColumns[i];
                cells[i].Should().Be(
                    rowCells[Array.IndexOf(rowHeader, column.ToLabel())],
                    "the {0} cell of a {1} row", column, row.Kind);
            }
        }
    }

    [Fact]
    public void FactsThatDoNotApply_AreEmptyCells_NeverZero()
    {
        var selector = new ColumnSelector([Column.Roll, Column.MatchLength, Column.Game, Column.MoveNumber, Column.Error]);

        selector.Serialize(TestRecords.Row(TestRecords.Cube())).Split(',')[0]
            .Should().BeEmpty("a cube decision has no roll");
        selector.Serialize(TestRecords.Row(TestRecords.CheckerPlay(
                position: TestRecords.Position(session: TestRecords.MoneySession()))))
            .Split(',')[1].Should().BeEmpty("a money session has no match length");
        selector.Serialize(TestRecords.Row(TestRecords.CheckerPlay(id: new XgpDecisionId("position.xgp"))))
            .Split(',')[2..4].Should().AllBe(string.Empty, "a standalone position belongs to no game");
        selector.Serialize(TestRecords.Row(RankingSplit.Played(RankingSplit.ShallowHigh), PlayRanking.DepthFirst))
            .Split(',')[4].Should().BeEmpty("a move the ranking does not score has no error");
    }

    [Fact]
    public void Numbers_AreCultureInvariant()
    {
        // A decimal comma would split a cell in two: the cells are written in
        // the invariant culture whatever the ambient one, as the row's own CSV.
        var original = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("de-DE");

            new ColumnSelector([Column.Error]).Serialize(Erred("Alice", 0.05)).Should().Be("0.05");
        }
        finally
        {
            CultureInfo.CurrentCulture = original;
        }
    }

    // -----------------------------------------------------------------------
    //  Undefined enum value — defensive throw at serialization time
    // -----------------------------------------------------------------------

    [Fact]
    public void Serialize_UndefinedColumnValue_Throws()
    {
        var selector = new ColumnSelector([(Column)999]);

        var act = () => selector.Serialize(TestRecords.Row());
        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    // -----------------------------------------------------------------------
    //  BuildCsv
    // -----------------------------------------------------------------------

    [Fact]
    public void BuildCsv_EmptyRows_ReturnsHeaderOnly()
    {
        var selector = new ColumnSelector([Column.Player, Column.Error]);

        var csv = selector.BuildCsv([]);

        csv.Should().StartWith("Player,Error");
        csv.Trim().Should().Be("Player,Error");
    }

    [Fact]
    public void BuildCsv_MultipleRows_IncludesHeaderAndAllRows()
    {
        var selector = new ColumnSelector([Column.Player, Column.Error]);
        var rows = new[]
        {
            Erred("Alice", 0.05),
            Erred("Bob", 0.10),
        };

        var csv = selector.BuildCsv(rows);
        var lines = csv.Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries);

        lines.Should().HaveCount(3); // header + 2 rows
        lines[0].Should().Be("Player,Error");
        lines[1].Should().Contain("Alice");
        lines[2].Should().Contain("Bob");
    }

    [Fact]
    public void BuildCsv_DefaultSelector_HeaderIsAllColumns()
    {
        var selector = new ColumnSelector();
        var csv = selector.BuildCsv([]);
        var firstLine = csv.Split(Environment.NewLine)[0];

        firstLine.Should().Be("Xgid,Error,MatchScore,MatchLength,Player,SourceFile,Game,MoveNumber,Roll,AnalysisDepth,Equity");
    }
}
