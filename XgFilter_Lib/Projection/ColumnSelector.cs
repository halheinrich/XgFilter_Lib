using System.Text;
using BgDataTypes_Lib;
using XgFilter_Lib.Enums;

namespace XgFilter_Lib.Projection;

/// <summary>
/// Selects and serializes a subset of <see cref="DecisionRow"/> columns
/// for CSV output. Callers specify the columns via <see cref="Column"/>
/// enum values; the header text comes from each member's
/// <c>[Description]</c> label. Default construction selects every
/// column in declaration order.
/// </summary>
public sealed class ColumnSelector
{
    /// <summary>Every defined column, in declaration order.</summary>
    public static readonly IReadOnlyList<Column> AllColumns =
        Enum.GetValues<Column>();

    private readonly List<Column> _selected;

    /// <summary>Creates a selector with every column active, in declaration order.</summary>
    public ColumnSelector() : this(AllColumns) { }

    /// <summary>Creates a selector with an explicit ordered column list.</summary>
    public ColumnSelector(IEnumerable<Column> columns)
    {
        _selected = new List<Column>(columns);
    }

    /// <summary>The ordered list of active columns.</summary>
    public IReadOnlyList<Column> SelectedColumns => _selected;

    /// <summary>CSV header row, joined from each selected column's label.</summary>
    public string Header => string.Join(",", _selected.Select(c => c.ToLabel()));

    /// <summary>Serializes a single row using the active columns.</summary>
    public string Serialize(DecisionRow row) =>
        string.Join(",", _selected.Select(c => GetValue(row, c)));

    /// <summary>Builds a complete CSV string (header + rows) for the given sequence.</summary>
    public string BuildCsv(IEnumerable<DecisionRow> rows)
    {
        var sb = new StringBuilder();
        sb.AppendLine(Header);
        foreach (var row in rows)
            sb.AppendLine(Serialize(row));
        return sb.ToString();
    }

    private static string GetValue(DecisionRow row, Column column) => column switch
    {
        Column.Xgid          => CsvEscape(row.Xgid),
        Column.Error         => row.Error.ToString("G6"),
        Column.MatchScore    => CsvEscape(row.MatchScore),
        Column.MatchLength   => row.MatchLength.ToString(),
        Column.Player        => CsvEscape(row.Player),
        Column.SourceFile    => CsvEscape(row.SourceFile ?? string.Empty),
        Column.Game          => row.Game.ToString(),
        Column.MoveNum       => row.MoveNum.ToString(),
        Column.Roll          => row.Roll.ToString(),
        Column.AnalysisDepth => CsvEscape(row.AnalysisDepth),
        Column.Equity        => row.Equity.ToString("G6"),
        _ => throw new ArgumentOutOfRangeException(
            nameof(column), column, "Unknown Column"),
    };

    private static string CsvEscape(string value)
    {
        if (value.Contains(',') || value.Contains('"') ||
            value.Contains('\n') || value.Contains('\r'))
            return $"\"{value.Replace("\"", "\"\"")}\"";
        return value;
    }
}
