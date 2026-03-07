using System.Text;
using ConvertXgToJson_Lib.Models;

namespace XgFilter_Lib.Projection;

/// <summary>
/// Explicit registry of all available CSV columns.
/// Maintains an ordered list of selected columns that drives both the CSV header
/// and row serialization — replacing <see cref="DecisionRow.ToCsvLine()"/> for
/// callers that need column projection.
/// </summary>
public sealed class ColumnSelector
{
    // -------------------------------------------------------------------
    //  Column registry
    // -------------------------------------------------------------------

    public static readonly IReadOnlyList<string> AllColumns =
    [
        "Xgid",
        "Error",
        "MatchScore",
        "MatchLength",
        "Player",
        "Match",
        "Game",
        "MoveNum",
        "Roll",
        "AnalysisDepth",
        "Equity",
    ];

    // -------------------------------------------------------------------
    //  State
    // -------------------------------------------------------------------

    private readonly List<string> _selected;

    // -------------------------------------------------------------------
    //  Construction
    // -------------------------------------------------------------------

    /// <summary>Creates a selector with all columns active (matches legacy behaviour).</summary>
    public ColumnSelector()
    {
        _selected = new List<string>(AllColumns);
    }

    /// <summary>Creates a selector with an explicit ordered column list.</summary>
    public ColumnSelector(IEnumerable<string> columns)
    {
        var invalid = columns.Where(c => !AllColumns.Contains(c)).ToList();
        if (invalid.Count > 0)
            throw new ArgumentException(
                $"Unknown column(s): {string.Join(", ", invalid)}", nameof(columns));

        _selected = new List<string>(columns);
    }

    // -------------------------------------------------------------------
    //  Public API
    // -------------------------------------------------------------------

    /// <summary>The ordered list of active columns.</summary>
    public IReadOnlyList<string> SelectedColumns => _selected;

    /// <summary>CSV header row for the active columns.</summary>
    public string Header => string.Join(",", _selected);

    /// <summary>Serializes a single row using the active columns.</summary>
    public string Serialize(DecisionRow row)
    {
        var values = _selected.Select(col => GetValue(row, col));
        return string.Join(",", values);
    }

    /// <summary>
    /// Builds a complete CSV string (header + rows) for the given sequence.
    /// Replaces <c>XgProcessingService.BuildCsv</c> when column projection is needed.
    /// </summary>
    public string BuildCsv(IEnumerable<DecisionRow> rows)
    {
        var sb = new StringBuilder();
        sb.AppendLine(Header);
        foreach (var row in rows)
            sb.AppendLine(Serialize(row));
        return sb.ToString();
    }

    // -------------------------------------------------------------------
    //  Value extraction
    // -------------------------------------------------------------------

    private static string GetValue(DecisionRow row, string column) => column switch
    {
        "Xgid"          => CsvEscape(row.Xgid),
        "Error"         => row.Error.ToString("G6"),
        "MatchScore"    => CsvEscape(row.MatchScore),
        "MatchLength"   => row.MatchLength.ToString(),
        "Player"        => CsvEscape(row.Player),
        "Match"         => CsvEscape(row.Match),
        "Game"          => row.Game.ToString(),
        "MoveNum"       => row.MoveNum.ToString(),
        "Roll"          => row.Roll.ToString(),
        "AnalysisDepth" => CsvEscape(row.AnalysisDepth),
        "Equity"        => row.Equity.ToString("G6"),
        _               => throw new InvalidOperationException($"Unhandled column: {column}"),
    };

    private static string CsvEscape(string value)
    {
        if (value.Contains(',') || value.Contains('"') ||
            value.Contains('\n') || value.Contains('\r'))
            return $"\"{value.Replace("\"", "\"\"")}\"";
        return value;
    }
}
