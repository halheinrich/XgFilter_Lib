using BgDataTypes_Lib;
using XgFilter_Lib.Filtering;
using XgFilter_Lib.Tests.Helpers;

namespace XgFilter_Lib.Tests.Filtering;

public class DiceRollFilterTests
{
    // RowShape.Roll is the two-digit form the producer stamps (high/low order is
    // irrelevant — DiceRoll canonicalizes); 0 means "no roll" and is only used
    // by the cube path, which routes through IsCube rather than the roll.

    // -----------------------------------------------------------------------
    //  Match / non-match
    // -----------------------------------------------------------------------

    [Fact]
    public void RollInSet_ReturnsTrue()
    {
        var filter = new DiceRollFilter([new DiceRoll(3, 1)]);
        AssertMatchesBoth(filter, new RowShape(Roll: 31), expected: true);
    }

    [Fact]
    public void RollNotInSet_ReturnsFalse()
    {
        var filter = new DiceRollFilter([new DiceRoll(3, 1)]);
        AssertMatchesBoth(filter, new RowShape(Roll: 52), expected: false);
    }

    // -----------------------------------------------------------------------
    //  OR semantics across the include-set
    // -----------------------------------------------------------------------

    [Fact]
    public void MultipleRolls_AnyMemberMatches()
    {
        var filter = new DiceRollFilter([new DiceRoll(3, 1), new DiceRoll(6, 6)]);

        AssertMatchesBoth(filter, new RowShape(Roll: 31), expected: true);
        AssertMatchesBoth(filter, new RowShape(Roll: 66), expected: true);
        AssertMatchesBoth(filter, new RowShape(Roll: 42), expected: false);
    }

    // -----------------------------------------------------------------------
    //  Doubles
    // -----------------------------------------------------------------------

    [Fact]
    public void Double_InSet_ReturnsTrue()
    {
        var filter = new DiceRollFilter([new DiceRoll(5, 5)]);
        AssertMatchesBoth(filter, new RowShape(Roll: 55), expected: true);
    }

    [Fact]
    public void Double_NotInSet_ReturnsFalse()
    {
        var filter = new DiceRollFilter([new DiceRoll(5, 5)]);
        AssertMatchesBoth(filter, new RowShape(Roll: 51), expected: false);
    }

    // -----------------------------------------------------------------------
    //  Unordered value-equality — the producer's canonicalization makes the
    //  dice order in the row and in the include-set irrelevant. Here the row's
    //  parser-order roll is low-first (13) while the include-set roll is built
    //  high-first (3,1); they must still match.
    // -----------------------------------------------------------------------

    [Fact]
    public void UnorderedRoll_LowFirstRow_MatchesHighFirstSet()
    {
        var filter = new DiceRollFilter([new DiceRoll(3, 1)]);
        AssertMatchesBoth(filter, new RowShape(Roll: 13), expected: true);
    }

    // -----------------------------------------------------------------------
    //  Cube rows carry no roll — always excluded by an active dice filter
    // -----------------------------------------------------------------------

    [Fact]
    public void CubeRow_ReturnsFalse()
    {
        var filter = new DiceRollFilter([new DiceRoll(3, 1)]);
        AssertMatchesBoth(filter, new RowShape(IsCube: true), expected: false);
    }

    // -----------------------------------------------------------------------
    //  Empty set — empty OR matches nothing (Build keeps this state out of the
    //  set; a directly-constructed empty filter still fails every row)
    // -----------------------------------------------------------------------

    [Fact]
    public void EmptySet_CheckerRow_ReturnsFalse()
    {
        var filter = new DiceRollFilter([]);
        AssertMatchesBoth(filter, new RowShape(Roll: 31), expected: false);
    }
}
