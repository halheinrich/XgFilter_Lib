namespace XgFilter_Lib.Enums;

/// <summary>
/// Board-derived classifications a position can carry. Categories are not
/// mutually exclusive — a single position may satisfy several (e.g. Contact
/// and InnerBoard631). All are determined from the on-roll-relative board
/// array alone; no XGID parsing is involved.
/// </summary>
public enum PositionType
{
    Contact,
    Race,
    Priming,
    Blitz,
    HoldingGame,
    InnerBoard631,
    InnerBoard54321,
}
