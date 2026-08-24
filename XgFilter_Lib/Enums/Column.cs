using System.ComponentModel;

namespace XgFilter_Lib.Enums;

/// <summary>
/// CSV columns available for projection via
/// <see cref="Projection.ColumnSelector"/>. Each member carries the
/// column's CSV-header text as a <see cref="DescriptionAttribute"/>
/// label; <see cref="EnumLabel.ToLabel{TEnum}(TEnum)"/> reads it. The
/// declaration order is the default output order.
/// </summary>
public enum Column
{
    /// <summary>The XGID position string identifying the decision.</summary>
    [Description("Xgid")]
    Xgid,

    /// <summary>The user's equity loss on the decision.</summary>
    [Description("Error")]
    Error,

    /// <summary>
    /// The match score at the decision (e.g. <c>"3a5a"</c>, <c>"1a5aC"</c>,
    /// <c>"moneyJ"</c>, <c>"moneyNJ"</c>) — the producer's rendering, which
    /// <see cref="BgDataTypes_Lib.DecisionRow.MatchScore"/> owns. A money row
    /// whose Jacoby rule was never stamped renders as the bare <c>"money"</c>,
    /// which states what is known; as a <em>filter</em> token that spelling is
    /// retired (see <see cref="Filtering.MatchScoreToken.RetiredMoney"/>).
    /// </summary>
    [Description("MatchScore")]
    MatchScore,

    /// <summary>The match length (0 for money sessions).</summary>
    [Description("MatchLength")]
    MatchLength,

    /// <summary>The on-roll player's name.</summary>
    [Description("Player")]
    Player,

    /// <summary>The source <c>.xg</c> / <c>.xgp</c> filename (without extension).</summary>
    [Description("SourceFile")]
    SourceFile,

    /// <summary>The 1-based game number within the match.</summary>
    [Description("Game")]
    Game,

    /// <summary>The 1-based move number within the game (standard-start games only).</summary>
    [Description("MoveNumber")]
    MoveNumber,

    /// <summary>The two-digit roll for checker plays (e.g. <c>31</c>); <c>0</c> for cube decisions.</summary>
    [Description("Roll")]
    Roll,

    /// <summary>XG's analysis-depth label for the decision (e.g. <c>"3-ply"</c>, <c>"XG Roller+"</c>).</summary>
    [Description("AnalysisDepth")]
    AnalysisDepth,

    /// <summary>The equity of the best play / cube action at this decision.</summary>
    [Description("Equity")]
    Equity,
}
