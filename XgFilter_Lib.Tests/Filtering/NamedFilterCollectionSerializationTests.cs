using System.Text.Json;
using BgDataTypes_Lib;
using XgFilter_Lib.Enums;
using XgFilter_Lib.Filtering;
using XgFilter_Lib.Patterns;

namespace XgFilter_Lib.Tests.Filtering;

public class NamedFilterCollectionSerializationTests
{
    /// <summary>A config touching every facet family, so round-trips exercise
    /// the full payload.</summary>
    private static FilterConfig RichConfig() => new()
    {
        Players = { "Alice", "Bob" },
        DecisionType = DecisionTypeOption.CheckerPlaysOnly,
        MatchScores = { "3a5a", "moneyJ" },
        ErrorMin = 0.05,
        ErrorMax = 0.5,
        MoveNumberMin = 2,
        MoveNumberMax = 10,
        ContactTypes = { ContactType.Contact },
        PositionTypes = { PositionType.InnerBoard631 },
        PlayTypes = { PlayType.Make20Pt },
        IncludeEvaluations = true,
        EvaluationLevels = { AnalysisLevel.Ply4 },
        IncludeRollouts = true,
        RolloutLevels = { AnalysisLevel.Ply3 },
        IncludeBookRollouts = true,
        BookRolloutLevels = { AnalysisLevel.XgRoller },
        DiceRolls = { new DiceRoll(3, 1) },
        PositionPattern = BoardPattern.Parse("[off,1,] [opp-off,0,0]"),
    };

    // -----------------------------------------------------------------------
    //  Round trips
    // -----------------------------------------------------------------------

    [Fact]
    public void RoundTrip_PreservesEverything()
    {
        var collection = NamedFilterCollection.Empty
            .With("Blitz", RichConfig())
            .With("calm", new FilterConfig());

        var restored = NamedFilterCollection.FromJson(collection.ToJson());

        restored.Names.Should().Equal("Blitz", "calm");
        restored.GetConfig("Blitz").ToJson().Should().Be(RichConfig().ToJson());
        restored.GetConfig("calm").ToJson().Should().Be(new FilterConfig().ToJson());
    }

    [Fact]
    public void RoundTrip_PreservesTheEmptyCollection()
    {
        var restored = NamedFilterCollection.FromJson(NamedFilterCollection.Empty.ToJson());

        restored.Count.Should().Be(0);
    }

    [Fact]
    public void Write_PinsTheWireShape()
    {
        // The envelope is pinned exactly; the config body is delegated to
        // FilterConfig.ToJson, whose form FilterConfig's own tests pin — the
        // converter embeds that output verbatim.
        var collection = NamedFilterCollection.Empty.With("Blitz", new FilterConfig());

        collection.ToJson().Should().Be(
            $$"""{"schemaVersion":1,"filters":[{"name":"Blitz","config":{{new FilterConfig().ToJson()}}}]}""");
    }

    [Fact]
    public void Write_PinsTheEmptyCollectionShape()
    {
        NamedFilterCollection.Empty.ToJson().Should().Be(
            """{"schemaVersion":1,"filters":[]}""");
    }

    [Fact]
    public void Write_OrdersEntriesCanonically_RegardlessOfAddOrder()
    {
        var collection = NamedFilterCollection.Empty
            .With("delta", new FilterConfig())
            .With("Alpha", new FilterConfig());
        var body = new FilterConfig().ToJson();

        collection.ToJson().Should().Be(
            $$"""{"schemaVersion":1,"filters":[{"name":"Alpha","config":{{body}}},{"name":"delta","config":{{body}}}]}""");
    }

    [Fact]
    public void Write_IsImmuneToConsumerNamingPolicy()
    {
        var options = new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseUpper };
        var collection = NamedFilterCollection.Empty.With("Blitz", new FilterConfig());

        var json = JsonSerializer.Serialize(collection, options);

        json.Should().Contain("\"schemaVersion\":1");
        json.Should().Contain("\"name\":\"Blitz\"");
        json.Should().Contain("\"Players\":");   // config body immune too
    }

    // -----------------------------------------------------------------------
    //  Fail-loud envelope reads
    // -----------------------------------------------------------------------

    private static string Valid() =>
        // A minimal valid document; tests splice mutations in via replacement.
        $$"""{"schemaVersion":1,"filters":[{"name":"Blitz","config":{{new FilterConfig().ToJson()}}}]}""";

    [Fact]
    public void Read_AcceptsUnsortedEntries_AndRecanonicalizes()
    {
        // Order is presentation, not semantics: a hand-reordered file is not
        // corruption. The read re-canonicalizes.
        var body = new FilterConfig().ToJson();
        var json =
            $$"""{"schemaVersion":1,"filters":[{"name":"delta","config":{{body}}},{"name":"Alpha","config":{{body}}}]}""";

        NamedFilterCollection.FromJson(json).Names.Should().Equal("Alpha", "delta");
    }

    [Fact]
    public void Read_RejectsNewerSchemaVersion_WithDistinguishedMessage()
    {
        var json = Valid().Replace("\"schemaVersion\":1", "\"schemaVersion\":2");

        var act = () => NamedFilterCollection.FromJson(json);

        act.Should().Throw<JsonException>().WithMessage("*newer*");
    }

    [Fact]
    public void Read_RejectsOlderSchemaVersion()
    {
        var json = Valid().Replace("\"schemaVersion\":1", "\"schemaVersion\":0");

        var act = () => NamedFilterCollection.FromJson(json);

        act.Should().Throw<JsonException>().WithMessage("*unsupported*");
    }

    [Fact]
    public void Read_RejectsMissingSchemaVersion()
    {
        var json = Valid().Replace("\"schemaVersion\":1,", "");

        var act = () => NamedFilterCollection.FromJson(json);

        act.Should().Throw<JsonException>().WithMessage("*schemaVersion*");
    }

    [Fact]
    public void Read_RejectsMissingFilters()
    {
        var act = () => NamedFilterCollection.FromJson("""{"schemaVersion":1}""");

        act.Should().Throw<JsonException>().WithMessage("*filters*");
    }

    [Fact]
    public void Read_RejectsUnknownTopLevelProperty()
    {
        var json = Valid().Replace("\"schemaVersion\":1", "\"schemaVersion\":1,\"extra\":1");

        var act = () => NamedFilterCollection.FromJson(json);

        act.Should().Throw<JsonException>().WithMessage("*extra*");
    }

    [Fact]
    public void Read_RejectsUnknownEntryProperty()
    {
        var json = Valid().Replace("\"name\":\"Blitz\"", "\"name\":\"Blitz\",\"extra\":1");

        var act = () => NamedFilterCollection.FromJson(json);

        act.Should().Throw<JsonException>().WithMessage("*extra*");
    }

    [Fact]
    public void Read_RejectsMissingName()
    {
        var json = $$"""{"schemaVersion":1,"filters":[{"config":{{new FilterConfig().ToJson()}}}]}""";

        var act = () => NamedFilterCollection.FromJson(json);

        act.Should().Throw<JsonException>().WithMessage("*name*");
    }

    [Fact]
    public void Read_RejectsMissingConfig()
    {
        var act = () => NamedFilterCollection.FromJson(
            """{"schemaVersion":1,"filters":[{"name":"Blitz"}]}""");

        act.Should().Throw<JsonException>().WithMessage("*config*");
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(" Blitz")]
    [InlineData("Blitz ")]
    public void Read_RejectsBlankOrUntrimmedName(string name)
    {
        // The same single-sourced rule as With: the wire enforces exactly the
        // in-memory name invariants.
        var json = Valid().Replace("\"name\":\"Blitz\"", $"\"name\":\"{name}\"");

        var act = () => NamedFilterCollection.FromJson(json);

        act.Should().Throw<JsonException>();
    }

    [Theory]
    [InlineData("Blitz")]   // exact duplicate
    [InlineData("blitz")]   // case-variant — same name per the name rule
    public void Read_RejectsDuplicateNames(string secondName)
    {
        var body = new FilterConfig().ToJson();
        var json =
            $$"""{"schemaVersion":1,"filters":[{"name":"Blitz","config":{{body}}},{"name":"{{secondName}}","config":{{body}}}]}""";

        var act = () => NamedFilterCollection.FromJson(json);

        act.Should().Throw<JsonException>().WithMessage("*Duplicate*");
    }

    // -----------------------------------------------------------------------
    //  Config bodies — tolerant payload, but corruption fails the file
    // -----------------------------------------------------------------------

    [Fact]
    public void Read_ToleratesUnknownConfigMembers_IgnoringThem()
    {
        // The strict-envelope/tolerant-payload split: a retired config facet
        // (e.g. a future PositionTypes drop) must never brick a saved
        // collection. Unknown members inside a config body are ignored,
        // exactly as FilterConfig's own deserialization ignores them.
        var json =
            """{"schemaVersion":1,"filters":[{"name":"Blitz","config":{"Players":["Alice"],"RetiredFacet":[1,2]}}]}""";

        var restored = NamedFilterCollection.FromJson(json);

        restored.GetConfig("Blitz").Players.Should().Equal("Alice");
    }

    [Theory]
    [InlineData("""{"DecisionType":"NotAnOption"}""")]        // invalid enum name
    [InlineData("""{"PositionPattern":"not-a-pattern"}""")]   // malformed pattern
    [InlineData("null")]                                      // null body
    [InlineData("5")]                                         // not an object
    public void Read_BadConfigBody_FailsTheWholeFile_NamingTheEntry(string body)
    {
        // Corruption, not evolution: a silently-reset saved filter would
        // filter nothing, which is worse than a loud error.
        var json = $$"""{"schemaVersion":1,"filters":[{"name":"Blitz","config":{{body}}}]}""";

        var act = () => NamedFilterCollection.FromJson(json);

        act.Should().Throw<JsonException>().WithMessage("*Blitz*");
    }

    // -----------------------------------------------------------------------
    //  FromJson / TryFromJson — the persistence trio
    // -----------------------------------------------------------------------

    [Fact]
    public void FromJson_RejectsNullToken()
    {
        var act = () => NamedFilterCollection.FromJson("null");

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void FromJson_RejectsNullString()
    {
        var act = () => NamedFilterCollection.FromJson(null!);

        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void TryFromJson_RestoresAValidCollection()
    {
        var collection = NamedFilterCollection.Empty.With("Blitz", RichConfig());

        var result = NamedFilterCollection.TryFromJson(collection.ToJson(), out var restored);

        result.Should().BeTrue();
        restored.Names.Should().Equal("Blitz");
        restored.GetConfig("Blitz").ToJson().Should().Be(RichConfig().ToJson());
    }

    [Theory]
    [InlineData(null)]                      // file never written
    [InlineData("null")]                    // literal null token
    [InlineData("not json")]                // malformed
    [InlineData("{\"schemaVersion\":2}")]   // contract violation
    public void TryFromJson_FallsBackToEmpty(string? json)
    {
        var result = NamedFilterCollection.TryFromJson(json, out var collection);

        result.Should().BeFalse();
        collection.Should().BeSameAs(NamedFilterCollection.Empty);
    }
}
