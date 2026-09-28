using BgDataTypes_Lib;
using XgFilter_Lib.Classification;
using XgFilter_Lib.Enums;

namespace XgFilter_Lib.Filtering;

/// <summary>
/// Passes checker plays whose (board, best after-board, player's after-board)
/// triple matches any of the selected play types — the best play being the
/// best under the ranking the view was built for
/// (<see cref="IDecisionFilterData.AfterBestBoard"/>). OR semantics across the
/// selected types; an empty type set yields false (empty OR). Unknown
/// <see cref="PlayType"/> values are rejected at construction rather than on
/// first dispatch.
///
/// <para>
/// <b>A play needs both after-boards, and none is ever substituted.</b> A cube
/// decision has neither (both are <see langword="null"/>: no play is made), so
/// it always fails. A checker play whose player's move is off the candidate
/// list has no after-board of its own
/// (<see cref="IDecisionFilterData.AfterPlayerBoard"/> is
/// <see langword="null"/>, halheinrich/backgammon#15), so it fails too: the
/// shape of the play is unknown, and reading another board in its place — the
/// best play's, or the board before the play — would classify a play that was
/// not made. <see langword="null"/> is the one spelling of "no after-board".
/// </para>
/// </summary>
internal sealed class PlayTypeFilter : IDecisionFilter
{
    /// <summary>
    /// Single source of truth for the <see cref="PlayType"/> →
    /// <see cref="IPlayTypeClassifier"/> correspondence. Adding a new
    /// play type means adding one entry here and a matching enum value
    /// in <see cref="PlayType"/>; nothing else inside the filter needs
    /// to change.
    /// </summary>
    private static readonly IReadOnlyDictionary<PlayType, IPlayTypeClassifier> _classifiers =
        new Dictionary<PlayType, IPlayTypeClassifier>
        {
            [PlayType.Make20Pt] = new Make20PtClassifier(),
        };

    private readonly HashSet<PlayType> _types;

    /// <summary>
    /// Creates a filter passing rows that match any of the selected
    /// <paramref name="types"/>. Throws <see cref="ArgumentOutOfRangeException"/>
    /// if <paramref name="types"/> contains an undefined enum value.
    /// </summary>
    public PlayTypeFilter(IEnumerable<PlayType> types)
    {
        _types = new HashSet<PlayType>(types);
        foreach (var type in _types)
            if (!Enum.IsDefined(type))
                throw new ArgumentOutOfRangeException(
                    nameof(types), type, "Unknown PlayType");
    }

    /// <summary>
    /// Returns <c>true</c> iff the decision has both after-boards and at least
    /// one selected <see cref="PlayType"/>'s classifier matches the (board,
    /// best after-board, player's after-board) triple. Returns <c>false</c>
    /// for a cube decision (no play was made) and for a checker play whose
    /// player's move is off the candidate list (no after-board of its own) —
    /// see the type remarks.
    /// </summary>
    public bool Matches(IDecisionFilterData data)
    {
        if (data.AfterBestBoard is not { } afterBest || data.AfterPlayerBoard is not { } afterPlayer)
            return false;

        foreach (var type in _types)
            if (_classifiers[type].Matches(data.Board, afterBest, afterPlayer))
                return true;
        return false;
    }
}
