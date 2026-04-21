using System.ComponentModel;
using XgFilter_Lib.Enums;
using XgFilter_Lib.Filtering;

namespace XgFilter_Lib.Tests.Enums;

public class EnumLabelTests
{
    // -----------------------------------------------------------------------
    //  Explicit label canaries — catch attribute-wiring regressions
    // -----------------------------------------------------------------------

    [Fact]
    public void PlayType_Make20Pt_HasExpectedLabel()
    {
        PlayType.Make20Pt.ToLabel().Should().Be("Make 20-point?");
    }

    [Fact]
    public void PositionType_Contact_HasExpectedLabel()
    {
        PositionType.Contact.ToLabel().Should().Be("Contact");
    }

    [Fact]
    public void PositionType_Race_HasExpectedLabel()
    {
        PositionType.Race.ToLabel().Should().Be("Race");
    }

    [Fact]
    public void PositionType_InnerBoard631_HasExpectedLabel()
    {
        PositionType.InnerBoard631.ToLabel().Should().Be("Inner-board 6-3-1");
    }

    [Fact]
    public void PositionType_InnerBoard54321_HasExpectedLabel()
    {
        PositionType.InnerBoard54321.ToLabel().Should().Be("Inner-board 5-4-3-2-1");
    }

    [Fact]
    public void DecisionTypeOption_CheckerPlaysOnly_HasExpectedLabel()
    {
        DecisionTypeOption.CheckerPlaysOnly.ToLabel().Should().Be("Checker plays only");
    }

    [Fact]
    public void DecisionTypeOption_CubeOnly_HasExpectedLabel()
    {
        DecisionTypeOption.CubeOnly.ToLabel().Should().Be("Cube decisions only");
    }

    [Fact]
    public void DecisionTypeOption_Both_HasExpectedLabel()
    {
        DecisionTypeOption.Both.ToLabel().Should().Be("Both checker and cube");
    }

    // -----------------------------------------------------------------------
    //  Exhaustive round-trip — any new enum value missing [Description] fails
    //  via the throw-on-missing contract; the non-empty check just guards
    //  against an accidental [Description("")].
    // -----------------------------------------------------------------------

    [Fact]
    public void EveryPlayType_HasNonEmptyLabel()
    {
        foreach (var value in Enum.GetValues<PlayType>())
            value.ToLabel().Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public void EveryPositionType_HasNonEmptyLabel()
    {
        foreach (var value in Enum.GetValues<PositionType>())
            value.ToLabel().Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public void EveryDecisionTypeOption_HasNonEmptyLabel()
    {
        foreach (var value in Enum.GetValues<DecisionTypeOption>())
            value.ToLabel().Should().NotBeNullOrWhiteSpace();
    }

    // -----------------------------------------------------------------------
    //  Unknown / unannotated — contract: throw
    // -----------------------------------------------------------------------

    [Fact]
    public void UnknownPlayTypeValue_Throws()
    {
        var act = () => ((PlayType)999).ToLabel();
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void UnknownPositionTypeValue_Throws()
    {
        var act = () => ((PositionType)999).ToLabel();
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void UnknownDecisionTypeOptionValue_Throws()
    {
        var act = () => ((DecisionTypeOption)999).ToLabel();
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void DeclaredMemberWithoutDescription_Throws()
    {
        var act = () => Unlabelled.NoAttribute.ToLabel();
        act.Should().Throw<ArgumentException>();
    }

    // Local test-only enum: a declared member without [Description] should
    // surface as a loud failure, not silently degrade to ToString().
    private enum Unlabelled
    {
        NoAttribute,
    }
}
