using BgDataTypes_Lib;

namespace XgFilter_Lib.Filtering;

/// <summary>
/// Passes rows whose rolled dice are in the selected include-set. OR semantics:
/// a row passes when its <see cref="IDecisionFilterData.Dice"/> value-equals any
/// selected <see cref="DiceRoll"/>. The producer stamps dice in canonical
/// unordered form (<see cref="DiceRoll"/> canonicalizes 3-1 and 1-3 to one
/// value), so this filter needs no normalization of its own — set membership
/// over the record-struct value equality does the work.
///
/// <para>
/// Cube rows always fail: a cube is offered before the on-roll player rolls, so
/// <see cref="IDecisionFilterData.Dice"/> is null and no roll exists to match.
/// This is the same drop-don't-pass posture <see cref="PlayTypeFilter"/> applies
/// to cube rows (where no play was made) and <see cref="ErrorRangeFilter"/>
/// applies to a null <see cref="IDecisionFilterData.FilterError"/>. An empty
/// include-set yields false for every row (empty OR, matching
/// <see cref="PlayTypeFilter"/>); <see cref="FilterConfig.Build"/> keeps the
/// facet inactive by skipping the add rather than ever materializing an
/// empty-set filter, so an active filter always carries at least one roll.
/// </para>
///
/// <para>
/// Deliberately implements only <see cref="IDecisionFilter.Matches"/> — no
/// <see cref="IMatchFilter"/> and no <c>ShouldAdvance*</c> overrides. The dice
/// are not knowable from a match or game header (a roll is a per-decision
/// property) and are not monotonic within a game (successive decisions carry
/// unrelated rolls), so there is no sound early-exit to offer — the
/// <see cref="AnalysisDepthFilter"/> reasoning.
/// </para>
///
/// <para>
/// Unlike the enum-based facets, <see cref="DiceRoll"/> is validated by
/// construction (each face is checked 1–6), so there is no unknown-value case to
/// reject here — an ill-formed roll cannot reach the include-set in the first
/// place.
/// </para>
/// </summary>
internal sealed class DiceRollFilter : IDecisionFilter
{
    private readonly HashSet<DiceRoll> _rolls;

    /// <summary>
    /// Creates a filter passing rows whose <see cref="IDecisionFilterData.Dice"/>
    /// is any of <paramref name="rolls"/>. An empty sequence yields a filter that
    /// matches nothing (empty OR); <see cref="FilterConfig.Build"/> avoids that
    /// state by not adding the filter when no rolls are selected.
    /// </summary>
    /// <param name="rolls">The admitted rolls, in any dice order (canonicalized by
    /// <see cref="DiceRoll"/>).</param>
    /// <exception cref="ArgumentNullException"><paramref name="rolls"/> is null.</exception>
    public DiceRollFilter(IEnumerable<DiceRoll> rolls)
    {
        _rolls = new HashSet<DiceRoll>(rolls);
    }

    /// <summary>
    /// Returns <c>true</c> iff the row carries a roll
    /// (<see cref="IDecisionFilterData.Dice"/> is non-null — i.e. it is a checker
    /// decision) and that roll is in the selected set. Cube rows (null dice)
    /// always return <c>false</c>.
    /// </summary>
    public bool Matches(IDecisionFilterData data) =>
        data.Dice is { } dice && _rolls.Contains(dice);
}
