using BgDataTypes_Lib;
using XgFilter_Lib.Enums;
using XgFilter_Lib.Filtering;
using XgFilter_Lib.Patterns;

namespace XgFilter_Lib.Tests.Filtering;

public class NamedFilterCollectionTests
{
    /// <summary>A config touching every facet family, so snapshot and
    /// equivalence checks exercise the full value.</summary>
    private static FilterConfig RichConfig() => new()
    {
        Players = { "Alice", "Bob" },
        DecisionType = DecisionTypeOption.CheckerPlaysOnly,
        MatchScores = { "3a5a", "money" },
        ErrorMin = 0.05,
        ErrorMax = 0.5,
        MoveNumberMin = 2,
        MoveNumberMax = 10,
        ContactTypes = { ContactType.Contact },
        PositionTypes = { PositionType.InnerBoard631 },
        PlayTypes = { PlayType.Make20Pt },
        AnalysisLevels = { AnalysisLevel.Ply4 },
        IncludeRollouts = true,
        DiceRolls = { new DiceRoll(3, 1) },
        PositionPattern = BoardPattern.Parse("[off,1,] [opp-off,0,0]"),
    };

    // -----------------------------------------------------------------------
    //  Empty
    // -----------------------------------------------------------------------

    [Fact]
    public void Empty_HasNoFilters()
    {
        NamedFilterCollection.Empty.Count.Should().Be(0);
        NamedFilterCollection.Empty.Names.Should().BeEmpty();
    }

    // -----------------------------------------------------------------------
    //  With — add, replace, invariants
    // -----------------------------------------------------------------------

    [Fact]
    public void With_AddsAFilter()
    {
        var collection = NamedFilterCollection.Empty.With("Blitz", RichConfig());

        collection.Count.Should().Be(1);
        collection.Names.Should().Equal("Blitz");
        collection.Contains("Blitz").Should().BeTrue();
    }

    [Fact]
    public void With_ReturnsANewInstance_LeavingTheOriginalUntouched()
    {
        var original = NamedFilterCollection.Empty.With("Blitz", new FilterConfig());

        var extended = original.With("Calm", new FilterConfig());

        extended.Should().NotBeSameAs(original);
        original.Names.Should().Equal("Blitz");
        extended.Names.Should().Equal("Blitz", "Calm");
    }

    [Fact]
    public void Names_AreSortedCaseInsensitively_RegardlessOfAddOrder()
    {
        var collection = NamedFilterCollection.Empty
            .With("delta", new FilterConfig())
            .With("Alpha", new FilterConfig())
            .With("charlie", new FilterConfig())
            .With("Bravo", new FilterConfig());

        collection.Names.Should().Equal("Alpha", "Bravo", "charlie", "delta");
    }

    [Fact]
    public void With_ExistingNameInDifferentCase_Replaces_NewSpellingAndConfigWin()
    {
        var collection = NamedFilterCollection.Empty
            .With("Blitz", new FilterConfig())
            .With("blitz", RichConfig());

        collection.Count.Should().Be(1);
        collection.Names.Should().Equal("blitz");   // last write wins for spelling too
        collection.GetConfig("BLITZ").ToJson().Should().Be(RichConfig().ToJson());
    }

    [Fact]
    public void With_NullName_Throws()
    {
        var act = () => NamedFilterCollection.Empty.With(null!, new FilterConfig());

        act.Should().Throw<ArgumentNullException>();
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(" Blitz")]
    [InlineData("Blitz ")]
    public void With_BlankOrUntrimmedName_Throws(string name)
    {
        var act = () => NamedFilterCollection.Empty.With(name, new FilterConfig());

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void With_NullConfig_Throws()
    {
        var act = () => NamedFilterCollection.Empty.With("Blitz", null!);

        act.Should().Throw<ArgumentNullException>();
    }

    // -----------------------------------------------------------------------
    //  Without
    // -----------------------------------------------------------------------

    [Fact]
    public void Without_RemovesCaseInsensitively()
    {
        var collection = NamedFilterCollection.Empty.With("Blitz", new FilterConfig());

        var removed = collection.Without("BLITZ");

        removed.Count.Should().Be(0);
        collection.Count.Should().Be(1);   // original untouched
    }

    [Fact]
    public void Without_MissingName_IsANoOpReturningTheSameInstance()
    {
        var collection = NamedFilterCollection.Empty.With("Blitz", new FilterConfig());

        collection.Without("nope").Should().BeSameAs(collection);
        NamedFilterCollection.Empty.Without("nope").Should().BeSameAs(NamedFilterCollection.Empty);
    }

    [Fact]
    public void Without_NullName_Throws()
    {
        var act = () => NamedFilterCollection.Empty.Without(null!);

        act.Should().Throw<ArgumentNullException>();
    }

    // -----------------------------------------------------------------------
    //  Lookup — Contains / GetConfig / TryGetConfig
    // -----------------------------------------------------------------------

    [Fact]
    public void Contains_IsCaseInsensitive()
    {
        var collection = NamedFilterCollection.Empty.With("Blitz", new FilterConfig());

        collection.Contains("bLiTz").Should().BeTrue();
        collection.Contains("other").Should().BeFalse();
    }

    [Fact]
    public void Contains_NullName_Throws()
    {
        var act = () => NamedFilterCollection.Empty.Contains(null!);

        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void GetConfig_ReturnsAnEquivalentConfig_CaseInsensitively()
    {
        var collection = NamedFilterCollection.Empty.With("Blitz", RichConfig());

        collection.GetConfig("BLITZ").ToJson().Should().Be(RichConfig().ToJson());
    }

    [Fact]
    public void GetConfig_MissingName_Throws()
    {
        var act = () => NamedFilterCollection.Empty.GetConfig("nope");

        act.Should().Throw<KeyNotFoundException>().WithMessage("*nope*");
    }

    [Fact]
    public void GetConfig_NullName_Throws()
    {
        var act = () => NamedFilterCollection.Empty.GetConfig(null!);

        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void TryGetConfig_Hit_YieldsAnEquivalentConfig()
    {
        var collection = NamedFilterCollection.Empty.With("Blitz", RichConfig());

        var found = collection.TryGetConfig("blitz", out var config);

        found.Should().BeTrue();
        config!.ToJson().Should().Be(RichConfig().ToJson());
    }

    [Fact]
    public void TryGetConfig_Miss_YieldsNull()
    {
        var found = NamedFilterCollection.Empty.TryGetConfig("nope", out var config);

        found.Should().BeFalse();
        config.Should().BeNull();
    }

    // -----------------------------------------------------------------------
    //  Snapshot contract — the document stores values, not instances
    // -----------------------------------------------------------------------

    [Fact]
    public void With_SnapshotsOnIngress_LaterMutationOfTheCallersConfigDoesNotLeakIn()
    {
        var live = new FilterConfig();
        var collection = NamedFilterCollection.Empty.With("Blitz", live);

        live.Players.Add("Mallory");

        collection.GetConfig("Blitz").Players.Should().BeEmpty();
    }

    [Fact]
    public void GetConfig_SnapshotsOnEgress_MutatingARetrievedConfigDoesNotLeakBack()
    {
        var collection = NamedFilterCollection.Empty.With("Blitz", new FilterConfig());

        collection.GetConfig("Blitz").Players.Add("Mallory");

        collection.GetConfig("Blitz").Players.Should().BeEmpty();
    }

    [Fact]
    public void GetConfig_ReturnsAFreshInstancePerCall()
    {
        var collection = NamedFilterCollection.Empty.With("Blitz", new FilterConfig());

        collection.GetConfig("Blitz").Should().NotBeSameAs(collection.GetConfig("Blitz"));
    }
}
