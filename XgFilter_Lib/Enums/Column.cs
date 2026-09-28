using System.ComponentModel;

namespace XgFilter_Lib.Enums;

/// <summary>
/// CSV columns available for projection via
/// <see cref="Projection.ColumnSelector"/>. Each member carries the
/// column's CSV-header text as a <see cref="DescriptionAttribute"/>
/// label; <see cref="EnumLabel.ToLabel{TEnum}(TEnum)"/> reads it. The
/// declaration order is the default output order. A column whose fact does
/// not apply to a row is an empty cell, never 0.
/// </summary>
public enum Column
{
    /// <summary>The XGID position string identifying the decision.</summary>
    [Description("Xgid")]
    Xgid,

    /// <summary>
    /// The player's error under the row's ranking
    /// (<see cref="BgDataTypes_Lib.DecisionRow.Error"/>); empty when the
    /// player's result has none — a move the ranking does not score, or no
    /// move recorded.
    /// </summary>
    [Description("Error")]
    Error,

    /// <summary>
    /// The match score at the decision (e.g. <c>"3a5a"</c>, <c>"1a5aC"</c>,
    /// <c>"moneyJ"</c>, <c>"moneyNJ"</c>) — the producer's rendering, which
    /// <see cref="BgDataTypes_Lib.DecisionRow.MatchScore"/> owns. Every money
    /// row states its Jacoby rule, so the bare <c>"money"</c> never appears
    /// here; as a <em>filter</em> token that spelling is retired (see
    /// <see cref="Filtering.MatchScoreToken.RetiredMoney"/>).
    /// </summary>
    [Description("MatchScore")]
    MatchScore,

    /// <summary>The match length; empty for a money session, which has none.</summary>
    [Description("MatchLength")]
    MatchLength,

    /// <summary>The on-roll player's name; empty when the source recorded none.</summary>
    [Description("Player")]
    Player,

    /// <summary>The source <c>.xg</c> / <c>.xgp</c> filename (without extension).</summary>
    [Description("SourceFile")]
    SourceFile,

    /// <summary>
    /// The 1-based game number within the match; empty for a standalone
    /// position, which belongs to no game.
    /// </summary>
    [Description("Game")]
    Game,

    /// <summary>
    /// The 1-based move number within the game; empty for a standalone
    /// position, which belongs to no game.
    /// </summary>
    [Description("MoveNumber")]
    MoveNumber,

    /// <summary>The two-digit roll for checker plays (e.g. <c>31</c>); empty for a cube decision.</summary>
    [Description("Roll")]
    Roll,

    /// <summary>XG's analysis-depth label for the decision (e.g. <c>"3-ply"</c>, <c>"XG Roller+"</c>).</summary>
    [Description("AnalysisDepth")]
    AnalysisDepth,

    /// <summary>
    /// The equity of the analysis's best line: the best play's under the
    /// row's ranking, or a cube decision's no-double equity
    /// (<see cref="BgDataTypes_Lib.DecisionRow.Equity"/>).
    /// </summary>
    [Description("Equity")]
    Equity,
}
