using System.ComponentModel;
using BgDataTypes_Lib;
using XgFilter_Lib.Enums;

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
    public void ContactType_Contact_HasExpectedLabel()
    {
        ContactType.Contact.ToLabel().Should().Be("Contact");
    }

    [Fact]
    public void ContactType_Race_HasExpectedLabel()
    {
        ContactType.Race.ToLabel().Should().Be("Race");
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
    public void PositionType_VsTwoPlusUp_HasExpectedLabel()
    {
        PositionType.VsTwoPlusUp.ToLabel().Should().Be("Vs 2+ on bar");
    }

    [Fact]
    public void PositionType_Holding1386Vs20_HasExpectedLabel()
    {
        PositionType.Holding1386Vs20.ToLabel().Should().Be("Holding 13-8-6 vs 20");
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

    [Fact]
    public void Column_Xgid_HasExpectedLabel()
    {
        Column.Xgid.ToLabel().Should().Be("Xgid");
    }

    [Fact]
    public void Column_MoveNumber_HasExpectedLabel()
    {
        Column.MoveNumber.ToLabel().Should().Be("MoveNumber");
    }

    // The two depth-taxonomy enums are owned by BgDataTypes_Lib but their
    // UI labels are read through this same helper; a missing [Description]
    // on either must surface as a loud failure, per the shared convention.

    [Fact]
    public void AnalysisMode_Evaluation_HasExpectedLabel()
    {
        AnalysisMode.Evaluation.ToLabel().Should().Be("Evaluation");
    }

    [Fact]
    public void AnalysisMode_BookRollout_HasExpectedLabel()
    {
        AnalysisMode.BookRollout.ToLabel().Should().Be("Book rollout");
    }

    [Fact]
    public void AnalysisLevel_Ply4_HasExpectedLabel()
    {
        AnalysisLevel.Ply4.ToLabel().Should().Be("4-ply");
    }

    [Fact]
    public void AnalysisLevel_XgRollerPlusPlus_HasExpectedLabel()
    {
        AnalysisLevel.XgRollerPlusPlus.ToLabel().Should().Be("XG Roller++");
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
    public void EveryContactType_HasNonEmptyLabel()
    {
        foreach (var value in Enum.GetValues<ContactType>())
            value.ToLabel().Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public void EveryDecisionTypeOption_HasNonEmptyLabel()
    {
        foreach (var value in Enum.GetValues<DecisionTypeOption>())
            value.ToLabel().Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public void EveryColumn_HasNonEmptyLabel()
    {
        foreach (var value in Enum.GetValues<Column>())
            value.ToLabel().Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public void EveryAnalysisMode_HasNonEmptyLabel()
    {
        foreach (var value in Enum.GetValues<AnalysisMode>())
            value.ToLabel().Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public void EveryAnalysisLevel_HasNonEmptyLabel()
    {
        foreach (var value in Enum.GetValues<AnalysisLevel>())
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
    public void UnknownContactTypeValue_Throws()
    {
        var act = () => ((ContactType)999).ToLabel();
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void UnknownDecisionTypeOptionValue_Throws()
    {
        var act = () => ((DecisionTypeOption)999).ToLabel();
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void UnknownColumnValue_Throws()
    {
        var act = () => ((Column)999).ToLabel();
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
