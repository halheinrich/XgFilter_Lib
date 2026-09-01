using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;
using BgDataTypes_Lib;
using XgFilter_Lib.Enums;
using XgFilter_Lib.Filtering;

namespace XgFilter_Lib.Tests.Filtering;

/// <summary>
/// Pins the string-token-exact contract of a saved filter's enum members
/// (halheinrich/backgammon#164). A <see cref="FilterConfig"/> is a durable
/// payload: it is written to disk and read back later, possibly across a
/// release that renumbered an enum. Its reader is therefore the inverse of its
/// writer — declaration names in, declaration names out, numeric ordinals
/// rejected.
///
/// <para><see cref="AnalysisLevel"/> is the reason this matters rather than
/// being theoretical. Its declaration order is contractual and its ply and
/// XG Roller families interleave, so inserting a member renumbers every member
/// above it — which happened on 2026-08-28 when <c>Ply3Red</c> landed. Writing
/// names is what makes such a renumber safe, and that only holds if the reader
/// refuses numbers.</para>
///
/// <para>The strictness is inherited from each enum's own type-level
/// attribute — <see cref="AnalysisLevel"/>'s, owned by BgDataTypes_Lib, and
/// this library's four <c>StrictJsonStringEnumConverter</c> bundles. The seam
/// that reads a saved filter registers nothing of its own and has not since
/// the halheinrich/backgammon#16 dedupe, which is deliberate: an options-level
/// converter outranks a type attribute — measured, and pinned in
/// BgDataTypes_Lib's own suite — so a registration here would both defeat the
/// attributes and mask their removal. Since halheinrich/backgammon#129 leg 4
/// that seam is a source-generated
/// <c>JsonTypeInfo</c> off <c>XgFilterJsonContext</c> rather than an options
/// object, which changes nothing here: the attributes still carry it, and
/// <c>XgFilterJsonContextTests</c> pins the same rejections on that
/// path.</para>
/// </summary>
public class EnumTokenStrictnessTests
{
    // ------------------------------------------------------------------ //
    //  Names accepted — the writer's inverse, unchanged
    // ------------------------------------------------------------------ //

    [Fact]
    public void FromJson_DeclarationNames_AreAccepted()
    {
        var restored = FilterConfig.FromJson(
            """
            {"DecisionType":"CubeOnly","EvaluationLevels":["Ply3Red","XgRoller"],
             "PositionTypes":["VsTwoPlusUp"],"ContactTypes":["Race"],"PlayTypes":["Make20Pt"]}
            """);

        restored.DecisionType.Should().Be(DecisionTypeOption.CubeOnly);
        restored.EvaluationLevels.Should().Equal(AnalysisLevel.Ply3Red, AnalysisLevel.XgRoller);
        restored.PositionTypes.Should().Equal(PositionType.VsTwoPlusUp);
        restored.ContactTypes.Should().Equal(ContactType.Race);
        restored.PlayTypes.Should().Equal(PlayType.Make20Pt);
    }

    /// <summary>
    /// Every member of every enum a saved filter can carry survives a
    /// round-trip by name, so the strictness costs no legitimate token.
    /// </summary>
    [Fact]
    public void EveryAnalysisLevel_RoundTripsThroughASavedFilter()
    {
        var config = new FilterConfig();
        foreach (AnalysisLevel level in Enum.GetValues<AnalysisLevel>())
        {
            config.EvaluationLevels.Add(level);
        }

        var restored = FilterConfig.FromJson(config.ToJson());

        restored.EvaluationLevels.Should().Equal(config.EvaluationLevels);
    }

    // ------------------------------------------------------------------ //
    //  Ordinals rejected — the hazard being closed
    // ------------------------------------------------------------------ //

    [Theory]
    [InlineData("""{"DecisionType":1}""")]
    [InlineData("""{"EvaluationLevels":[1]}""")]
    [InlineData("""{"RolloutLevels":[4]}""")]
    [InlineData("""{"BookRolloutLevels":[0]}""")]
    [InlineData("""{"PositionTypes":[0]}""")]
    [InlineData("""{"ContactTypes":[0]}""")]
    [InlineData("""{"PlayTypes":[0]}""")]
    public void FromJson_NumericOrdinal_IsRejected(string json) =>
        Assert.Throws<JsonException>(() => FilterConfig.FromJson(json));

    /// <summary>
    /// An undefined ordinal is rejected too — the default converter would have
    /// produced a level that is not any member at all.
    /// </summary>
    [Fact]
    public void FromJson_UndefinedOrdinal_IsRejected() =>
        Assert.Throws<JsonException>(
            () => FilterConfig.FromJson("""{"EvaluationLevels":[99]}"""));

    /// <summary>
    /// The concrete regression this closes, spelled out: ordinal 4 named
    /// <see cref="AnalysisLevel.Ply4"/> before <c>Ply3Red</c> was inserted and
    /// names <see cref="AnalysisLevel.XgRoller"/> after it. A filter saved with
    /// that ordinal now fails loudly instead of silently changing which level
    /// the user filtered on.
    /// </summary>
    [Fact]
    public void FromJson_OrdinalThatChangedMeaning_FailsRatherThanRebinding()
    {
        ((int)AnalysisLevel.XgRoller).Should().Be(5, "Ply3Red's insertion shifted the ladder");

        Assert.Throws<JsonException>(
            () => FilterConfig.FromJson("""{"EvaluationLevels":[5]}"""));
    }

    // ------------------------------------------------------------------ //
    //  Through the durable collection reader
    // ------------------------------------------------------------------ //

    /// <summary>
    /// The rejection reaches the real saved-collection file reader, which
    /// routes every nested config through <c>FilterConfig.FromJson</c>: an
    /// ordinal inside one entry fails the file into the "invalid config body"
    /// corruption funnel, naming the offending filter, rather than restoring a
    /// filter the user never saved.
    /// </summary>
    [Fact]
    public void NamedFilterCollection_OrdinalInAnEntry_FailsNamingThatEntry()
    {
        string file =
            """
            {"schemaVersion":1,"filters":[
              {"name":"Mine","config":{"EvaluationLevels":[1]}}
            ]}
            """;

        var ex = Assert.Throws<JsonException>(() => NamedFilterCollection.FromJson(file));

        ex.Message.Should().Contain("Mine");
    }

    /// <summary>
    /// And the same file with the level spelled as a name restores cleanly —
    /// the failure above is about token kind, not about the reader being
    /// broken.
    /// </summary>
    [Fact]
    public void NamedFilterCollection_NamedLevelInAnEntry_Restores()
    {
        string file =
            """
            {"schemaVersion":1,"filters":[
              {"name":"Mine","config":{"EvaluationLevels":["Ply2"]}}
            ]}
            """;

        var restored = NamedFilterCollection.FromJson(file);

        restored.GetConfig("Mine").EvaluationLevels.Should().Equal(AnalysisLevel.Ply2);
    }

    // ------------------------------------------------------------------ //
    //  The guarantee travels to a consumer's own options
    // ------------------------------------------------------------------ //

    /// <summary>
    /// The property that makes the type-level attributes worth having, and the
    /// reason halheinrich/backgammon#37 was fixed on the enums rather than by
    /// exposing this library's canonical options: a <see cref="FilterConfig"/>
    /// crosses wires this library does not own — ExtractFromXgToCsv POSTs one to
    /// its local server, bound by ASP.NET Core's stock options — and under those
    /// the enums must still be names. Before the attributes they crossed as bare
    /// integer ordinals.
    /// </summary>
    [Fact]
    public void UnderForeignOptions_EnumsStillCrossAsNames()
    {
        var foreign = new JsonSerializerOptions();

        var config = new FilterConfig
        {
            DecisionType = DecisionTypeOption.CubeOnly,
            ContactTypes = { ContactType.Race },
            PositionTypes = { PositionType.VsTwoPlusUp },
            PlayTypes = { PlayType.Make20Pt },
            EvaluationLevels = { AnalysisLevel.Ply3Red },
        };

        var json = JsonSerializer.Serialize(config, foreign);

        json.Should().Contain("\"CubeOnly\"");
        json.Should().Contain("\"Race\"");
        json.Should().Contain("\"VsTwoPlusUp\"");
        json.Should().Contain("\"Make20Pt\"");
        json.Should().Contain("\"Ply3Red\"");

        var restored = JsonSerializer.Deserialize<FilterConfig>(json, foreign)!;

        restored.DecisionType.Should().Be(DecisionTypeOption.CubeOnly);
        restored.EvaluationLevels.Should().Equal(AnalysisLevel.Ply3Red);
    }

    /// <summary>
    /// And the rejection travels with it: an ordinal payload fails under a
    /// consumer's stock options too, not only through
    /// <see cref="FilterConfig.FromJson"/>.
    /// </summary>
    [Theory]
    [InlineData("{\"DecisionType\":1}")]
    [InlineData("{\"ContactTypes\":[0]}")]
    [InlineData("{\"PositionTypes\":[0]}")]
    [InlineData("{\"PlayTypes\":[0]}")]
    [InlineData("{\"EvaluationLevels\":[5]}")]
    public void UnderForeignOptions_OrdinalIsStillRejected(string json) =>
        Assert.Throws<JsonException>(
            () => JsonSerializer.Deserialize<FilterConfig>(json, new JsonSerializerOptions()));

    /// <summary>
    /// The attributes are the sole enforcement, this library's own seam
    /// registering nothing (the halheinrich/backgammon#16 dedupe), so this pins
    /// the precedence fact that makes that dedupe safe to reason about: a
    /// consumer CAN still lower the floor by registering a loose converter of
    /// its own. The attribute is what a consumer gets for free, not a ceiling it
    /// cannot override.
    /// </summary>
    [Fact]
    public void AConsumersOwnLooseRegistration_StillOutranksTheAttribute()
    {
        var loose = new JsonSerializerOptions
        {
            Converters = { new JsonStringEnumConverter() },
        };

        JsonSerializer.Deserialize<FilterConfig>("{\"DecisionType\":1}", loose)!
            .DecisionType.Should().Be(DecisionTypeOption.CubeOnly);
    }

    // ------------------------------------------------------------------ //
    //  Written bytes unchanged
    // ------------------------------------------------------------------ //

    /// <summary>
    /// The writer is untouched by the tightening: declaration names, exactly as
    /// every saved filter already on disk spells them. Pinned per member so a
    /// naming policy could never be introduced here unnoticed.
    /// </summary>
    [Fact]
    public void ToJson_WritesDeclarationNames_Unchanged()
    {
        foreach (AnalysisLevel level in Enum.GetValues<AnalysisLevel>())
        {
            var json = new FilterConfig { EvaluationLevels = { level } }.ToJson();

            json.Should().Contain(
                $"\"{level}\"",
                $"{level} must ride the wire as its declaration name, not "
                + Convert.ToInt32(level, CultureInfo.InvariantCulture));
        }
    }
}
