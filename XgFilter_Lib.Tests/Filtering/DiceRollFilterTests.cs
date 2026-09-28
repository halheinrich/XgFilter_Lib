using BgDataTypes_Lib;
using BgDataTypes_Lib.TestSupport;
using XgFilter_Lib.Filtering;

namespace XgFilter_Lib.Tests.Filtering;

public class DiceRollFilterTests
{
    /// <summary>
    /// A checker play from the standard start whose roll is
    /// <paramref name="first"/> then <paramref name="second"/>, in rolled order
    /// (the order is irrelevant to the filter — <see cref="DiceRoll"/>
    /// canonicalizes). A cube decision has no roll at all.
    /// </summary>
    private static CheckerPlayDecision Rolled(int first, int second) =>
        TestRecords.CheckerPlay(decision: TestRecords.CheckerPlayData(dice: [first, second]));

    // -----------------------------------------------------------------------
    //  Match / non-match
    // -----------------------------------------------------------------------

    [Fact]
    public void RollInSet_ReturnsTrue()
    {
        var filter = new DiceRollFilter([new DiceRoll(3, 1)]);
        AssertMatchesBoth(filter, Rolled(3, 1), expected: true);
    }

    [Fact]
    public void RollNotInSet_ReturnsFalse()
    {
        var filter = new DiceRollFilter([new DiceRoll(3, 1)]);
        AssertMatchesBoth(filter, Rolled(5, 2), expected: false);
    }

    // -----------------------------------------------------------------------
    //  OR semantics across the include-set
    // -----------------------------------------------------------------------

    [Fact]
    public void MultipleRolls_AnyMemberMatches()
    {
        var filter = new DiceRollFilter([new DiceRoll(3, 1), new DiceRoll(6, 6)]);

        AssertMatchesBoth(filter, Rolled(3, 1), expected: true);
        AssertMatchesBoth(filter, Rolled(6, 6), expected: true);
        AssertMatchesBoth(filter, Rolled(4, 2), expected: false);
    }

    // -----------------------------------------------------------------------
    //  Doubles
    // -----------------------------------------------------------------------

    [Fact]
    public void Double_InSet_ReturnsTrue()
    {
        var filter = new DiceRollFilter([new DiceRoll(5, 5)]);
        AssertMatchesBoth(filter, Rolled(5, 5), expected: true);
    }

    [Fact]
    public void Double_NotInSet_ReturnsFalse()
    {
        var filter = new DiceRollFilter([new DiceRoll(5, 5)]);
        AssertMatchesBoth(filter, Rolled(5, 1), expected: false);
    }

    // -----------------------------------------------------------------------
    //  Unordered value-equality — the producer's canonicalization makes the
    //  dice order in the record and in the include-set irrelevant. Here the
    //  record's rolled order is low-first (1, 3) while the include-set roll is
    //  built high-first (3,1); they must still match.
    // -----------------------------------------------------------------------

    [Fact]
    public void UnorderedRoll_LowFirstRow_MatchesHighFirstSet()
    {
        var filter = new DiceRollFilter([new DiceRoll(3, 1)]);
        AssertMatchesBoth(filter, Rolled(1, 3), expected: true);
    }

    // -----------------------------------------------------------------------
    //  A cube decision carries no roll — always excluded by an active dice filter
    // -----------------------------------------------------------------------

    [Fact]
    public void CubeDecision_ReturnsFalse()
    {
        var filter = new DiceRollFilter([new DiceRoll(3, 1)]);
        AssertMatchesBoth(filter, TestRecords.Cube(), expected: false);
    }

    // -----------------------------------------------------------------------
    //  Empty set — empty OR matches nothing (Build keeps this state out of the
    //  set; a directly-constructed empty filter still fails every row)
    // -----------------------------------------------------------------------

    [Fact]
    public void EmptySet_CheckerPlay_ReturnsFalse()
    {
        var filter = new DiceRollFilter([]);
        AssertMatchesBoth(filter, Rolled(3, 1), expected: false);
    }
}
