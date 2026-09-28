using System.Collections.Immutable;
using System.Globalization;
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
///
/// <para>
/// A cell is written as the row's own CSV writes it
/// (<see cref="DecisionRow.ToCsvLine"/>): a <see langword="null"/> column —
/// the other decision kind's, the other session kind's, or a fact that does
/// not apply, such as a standalone position's game and move number — is an
/// empty cell, never 0; and every number is written with the invariant
/// culture, whatever the ambient one, since a decimal comma would split a cell
/// in two.
/// </para>
/// </summary>
public sealed class ColumnSelector
{
    /// <summary>Every defined column, in declaration order. An immutable list, shared by every caller.</summary>
    public static IReadOnlyList<Column> AllColumns { get; } =
        ImmutableArray.Create(Enum.GetValues<Column>());

    private readonly ImmutableArray<Column> _selected;

    /// <summary>Creates a selector with every column active, in declaration order.</summary>
    public ColumnSelector() : this(AllColumns) { }

    /// <summary>Creates a selector with an explicit ordered column list.</summary>
    public ColumnSelector(IEnumerable<Column> columns)
    {
        _selected = [.. columns];
        SelectedColumns = _selected;
    }

    /// <summary>
    /// The ordered list of active columns — an immutable copy of what the
    /// constructor was given, so neither the caller's collection nor anything
    /// done to this list changes the selector.
    /// </summary>
    public IReadOnlyList<Column> SelectedColumns { get; }

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
        Column.Error         => Cell(row.Error),
        Column.MatchScore    => CsvEscape(row.MatchScore),
        Column.MatchLength   => Cell(row.MatchLength),
        Column.Player        => CsvEscape(row.Player),
        Column.SourceFile    => CsvEscape(row.SourceFile),
        Column.Game          => Cell(row.Game),
        Column.MoveNumber    => Cell(row.MoveNumber),
        Column.Roll          => Cell(row.Roll),
        Column.AnalysisDepth => CsvEscape(row.AnalysisDepth),
        Column.Equity        => Cell(row.Equity),
        _ => throw new ArgumentOutOfRangeException(
            nameof(column), column, "Unknown Column"),
    };

    /// <summary>A real-valued cell: <c>G6</c> in the invariant culture; empty for <see langword="null"/>.</summary>
    private static string Cell(double? value) =>
        value?.ToString("G6", CultureInfo.InvariantCulture) ?? string.Empty;

    /// <summary>An integer cell: plain digits in the invariant culture; empty for <see langword="null"/>.</summary>
    private static string Cell(int? value) =>
        value?.ToString(CultureInfo.InvariantCulture) ?? string.Empty;

    /// <summary>A text cell, quoted when it holds a separator; empty for <see langword="null"/> (none recorded).</summary>
    private static string CsvEscape(string? value)
    {
        if (value is null)
            return string.Empty;
        if (value.Contains(',') || value.Contains('"') ||
            value.Contains('\n') || value.Contains('\r'))
            return $"\"{value.Replace("\"", "\"\"")}\"";
        return value;
    }
}
