using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;
using BgDataTypes_Lib;
using XgFilter_Lib.Enums;
using XgFilter_Lib.Filtering;
using XgFilter_Lib.Patterns;

namespace XgFilter_Lib.Tests;

/// <summary>
/// The source-generation gate (halheinrich/backgammon#129 leg 4): the context
/// changes the mechanism, never the bytes. <c>FilterConfigTests</c>,
/// <c>NamedFilterCollectionSerializationTests</c>,
/// <c>EnumTokenStrictnessTests</c> and <c>BoardPatternWireSafetyTests</c> are
/// the outer byte gate — they pin the emitted documents against literal wire
/// strings written before any of this — and pass unchanged. This suite pins
/// the mechanism itself: the same document must come out whichever resolver
/// produces the metadata, every bundled converter must be honoured on the
/// source-generated path, and the context must cover this library's whole
/// wire surface.
/// </summary>
public class XgFilterJsonContextTests
{
    // -----------------------------------------------------------------------
    //  The metadata mechanisms. Each options object differs from the others in
    //  exactly one respect — where the JsonTypeInfo comes from. This library
    //  takes nothing else from options: its canonical seam registered no
    //  converters even before the context replaced it (the
    //  halheinrich/backgammon#16 dedupe in halheinrich/backgammon#37), so
    //  there is no policy for a resolver swap to disturb.
    // -----------------------------------------------------------------------

    /// <summary>The pre-change mechanism: runtime reflection.</summary>
    private static readonly JsonSerializerOptions ReflectionOptions =
        new() { TypeInfoResolver = new DefaultJsonTypeInfoResolver() };

    /// <summary>What this library ships: its own context, unchained.</summary>
    private static readonly JsonSerializerOptions ContextOnlyOptions =
        new() { TypeInfoResolver = XgFilterJsonContext.Default };

    /// <summary>
    /// The consumer shape the arc's composition pattern prescribes — this
    /// library's context ahead of BgDataTypes_Lib's, most-derived-first. This
    /// library needs no chain of its own (see
    /// <see cref="ThisReposContextAlone_ResolvesTheFullClosure"/>), but a
    /// consumer whose documents also carry BgDataTypes_Lib types will build
    /// exactly this, so the wire form has to survive it.
    /// </summary>
    private static readonly JsonSerializerOptions ChainedOptions =
        new()
        {
            TypeInfoResolver = JsonTypeInfoResolver.Combine(
                XgFilterJsonContext.Default,
                BgDataTypesJsonContext.Default),
        };

    /// <summary>
    /// The same chain in the other order — what a consumer that ordered its
    /// resolvers upstream-first would get, and the case where this context's
    /// transitive copy of <see cref="AnalysisLevel"/> / <see cref="DiceRoll"/>
    /// loses to BgDataTypes_Lib's own. Both orders must emit the same bytes;
    /// that is the shadow being benign rather than merely believed to be.
    /// </summary>
    private static readonly JsonSerializerOptions UpstreamFirstOptions =
        new()
        {
            TypeInfoResolver = JsonTypeInfoResolver.Combine(
                BgDataTypesJsonContext.Default,
                XgFilterJsonContext.Default),
        };

    // -----------------------------------------------------------------------
    //  Fixtures — fully populated: every facet of a FilterConfig occupied, so
    //  a resolver difference anywhere in the closure has somewhere to show.
    // -----------------------------------------------------------------------

    /// <summary>
    /// Every member of <see cref="FilterConfig"/> set to a non-default value:
    /// both string lists, the enum scalar, both nullable numeric pairs, all
    /// three enum lists, all three depth toggles with their level lists, the
    /// dice facet and the pattern facet. A default config would leave most of
    /// the closure untouched, so the byte-identity theories below run over
    /// both.
    /// </summary>
    private static FilterConfig PopulatedConfig() => new()
    {
        Players = { "Alice", "Bob" },
        DecisionType = DecisionTypeOption.CheckerPlaysOnly,
        MatchScores = { "3a5a", "moneyJ" },
        ErrorMin = 0.05,
        ErrorMax = 0.5,
        MoveNumberMin = 2,
        MoveNumberMax = 10,
        ContactTypes = { ContactType.Contact, ContactType.Race },
        PositionTypes = { PositionType.InnerBoard631, PositionType.VsTwoPlusUp },
        PlayTypes = { PlayType.Make20Pt },
        IncludeEvaluations = true,
        EvaluationLevels = { AnalysisLevel.Ply4, AnalysisLevel.Ply3Red },
        IncludeRollouts = true,
        RolloutLevels = { AnalysisLevel.Ply3 },
        IncludeBookRollouts = true,
        BookRolloutLevels = { AnalysisLevel.XgRoller },
        DiceRolls = { new DiceRoll(3, 1), new DiceRoll(6, 6) },
        PositionPattern = BoardPattern.Parse("[6,2,] [5,,-2] [off,1,] [opp-off,,-2]"),
    };

    public static TheoryData<string> Configs => new()
    {
        nameof(PopulatedConfig), "Default",
    };

    private static FilterConfig Config(string name) =>
        name == nameof(PopulatedConfig) ? PopulatedConfig() : new FilterConfig();

    /// <summary>
    /// A collection carrying both a fully-populated entry and a default one,
    /// under names whose canonical order differs from the insertion order.
    /// </summary>
    private static NamedFilterCollection PopulatedCollection() =>
        NamedFilterCollection.Empty
            .With("zeta", new FilterConfig())
            .With("Blitz", PopulatedConfig());

    public static TheoryData<string> Collections => new()
    {
        nameof(PopulatedCollection), "Empty",
    };

    private static NamedFilterCollection Collection(string name) =>
        name == nameof(PopulatedCollection) ? PopulatedCollection() : NamedFilterCollection.Empty;

    // -----------------------------------------------------------------------
    //  Byte identity — the invariant of the whole halheinrich/backgammon#129
    //  arc: source generation changes the mechanism, never the bytes.
    // -----------------------------------------------------------------------

    [Theory]
    [MemberData(nameof(Configs))]
    public void EveryResolver_EmitsTheSameConfig(string config)
    {
        var value = Config(config);

        var reflection = JsonSerializer.Serialize(value, TypeInfo<FilterConfig>(ReflectionOptions));

        // The shipped entry point, the context alone, and both chain orders.
        value.ToJson().Should().Be(reflection);
        Serialize(value, ContextOnlyOptions).Should().Be(reflection);
        Serialize(value, ChainedOptions).Should().Be(reflection);
        Serialize(value, UpstreamFirstOptions).Should().Be(reflection);
    }

    [Theory]
    [MemberData(nameof(Collections))]
    public void EveryResolver_EmitsTheSameCollection(string collection)
    {
        var value = Collection(collection);

        var reflection = JsonSerializer.Serialize(
            value, TypeInfo<NamedFilterCollection>(ReflectionOptions));

        value.ToJson().Should().Be(reflection);
        Serialize(value, ContextOnlyOptions).Should().Be(reflection);
        Serialize(value, ChainedOptions).Should().Be(reflection);
        Serialize(value, UpstreamFirstOptions).Should().Be(reflection);
    }

    /// <summary>
    /// The read half: a document deserialized through the source-generated
    /// metadata re-emits identically, so the mechanism change is invisible in
    /// both directions.
    /// </summary>
    [Theory]
    [MemberData(nameof(Configs))]
    public void SourceGeneratedConfigRoundTrip_IsStable(string config)
    {
        var json = Serialize(Config(config), ContextOnlyOptions);

        var restored = JsonSerializer.Deserialize(json, TypeInfo<FilterConfig>(ContextOnlyOptions))!;

        restored.Should().Be(Config(config));   // FilterConfig has full value equality
        Serialize(restored, ContextOnlyOptions).Should().Be(json);
    }

    [Theory]
    [MemberData(nameof(Collections))]
    public void SourceGeneratedCollectionRoundTrip_IsStable(string collection)
    {
        var json = Serialize(Collection(collection), ContextOnlyOptions);

        var restored = JsonSerializer.Deserialize(
            json, TypeInfo<NamedFilterCollection>(ContextOnlyOptions))!;

        Serialize(restored, ContextOnlyOptions).Should().Be(json);
    }

    /// <summary>
    /// Both tolerant restore paths route through the context. These are the
    /// two entry points whose failure mode is a silent fallback rather than an
    /// exception, so a broken resolver would show up not as a throw but as a
    /// saved filter that quietly went blank.
    /// </summary>
    [Fact]
    public void TryFromJson_RestoresThroughTheContext()
    {
        FilterConfig.TryFromJson(PopulatedConfig().ToJson(), out var config).Should().BeTrue();
        config.Should().Be(PopulatedConfig());

        FilterConfig.TryFromJson("not json {", out var fallback).Should().BeFalse();
        fallback.Should().Be(new FilterConfig());

        NamedFilterCollection.TryFromJson(PopulatedCollection().ToJson(), out var collection)
            .Should().BeTrue();
        collection.ToJson().Should().Be(PopulatedCollection().ToJson());

        NamedFilterCollection.TryFromJson("""{"schemaVersion":99,"filters":[]}""", out var empty)
            .Should().BeFalse();
        empty.Should().BeSameAs(NamedFilterCollection.Empty);
    }

    // -----------------------------------------------------------------------
    //  Converter respect on the source-generated path. A bundled type-level
    //  converter outranks whatever a resolver supplies, and that must stay
    //  true when the resolver is a source-generated context.
    // -----------------------------------------------------------------------

    /// <summary>
    /// Every self-describing member keeps its own token on the context path:
    /// the four halheinrich/backgammon#37 enums and BgDataTypes_Lib's
    /// <see cref="AnalysisLevel"/> as declaration names, <see cref="DiceRoll"/>
    /// as its two-digit token, <see cref="BoardPattern"/> as its bracket list.
    /// Each of those spellings comes from a different converter, and a
    /// resolver that dropped any of them would emit an ordinal, an object or a
    /// nested array here.
    /// </summary>
    [Fact]
    public void ContextPath_KeepsEveryBundledConverterToken()
    {
        var json = Serialize(PopulatedConfig(), ContextOnlyOptions);

        json.Should().Contain("\"DecisionType\":\"CheckerPlaysOnly\"");
        json.Should().Contain("\"ContactTypes\":[\"Contact\",\"Race\"]");
        json.Should().Contain("\"PositionTypes\":[\"InnerBoard631\",\"VsTwoPlusUp\"]");
        json.Should().Contain("\"PlayTypes\":[\"Make20Pt\"]");
        json.Should().Contain("\"EvaluationLevels\":[\"Ply4\",\"Ply3Red\"]");
        json.Should().Contain("\"DiceRolls\":[\"31\",\"66\"]");
        json.Should().Contain("\"PositionPattern\":\"[6,2,] [5,,-2] [off,1,] [opp-off,,-2]\"");
    }

    /// <summary>
    /// The collection's envelope is hand-written by
    /// <see cref="NamedFilterCollectionJsonConverter"/> — schema version,
    /// name-sorted entries, each body verbatim from
    /// <see cref="FilterConfig.ToJson"/>. A context that failed to honour the
    /// type-level converter would emit the type's shape instead, which has no
    /// public members at all.
    /// </summary>
    [Fact]
    public void ContextPath_CollectionKeepsItsPinnedEnvelope()
    {
        var json = Serialize(PopulatedCollection(), ContextOnlyOptions);

        json.Should().Be(
            $$"""{"schemaVersion":1,"filters":[{"name":"Blitz","config":{{PopulatedConfig().ToJson()}}},{"name":"zeta","config":{{new FilterConfig().ToJson()}}}]}""");
    }

    /// <summary>
    /// halheinrich/backgammon#164's strictness survives the mechanism change:
    /// numeric ordinals — defined, undefined, and the one that changed meaning
    /// when <see cref="AnalysisLevel.Ply3Red"/> was inserted — stay rejected on
    /// the source-generated path, for this library's own enums and for
    /// BgDataTypes_Lib's alike.
    /// </summary>
    [Theory]
    [InlineData("""{"DecisionType":1}""")]
    [InlineData("""{"ContactTypes":[0]}""")]
    [InlineData("""{"PositionTypes":[0]}""")]
    [InlineData("""{"PlayTypes":[0]}""")]
    [InlineData("""{"EvaluationLevels":[3]}""")]
    [InlineData("""{"RolloutLevels":[99]}""")]
    [InlineData("""{"BookRolloutLevels":[-1]}""")]
    public void ContextPath_RejectsNumericEnumOrdinals(string json) =>
        FluentActions.Invoking(
                () => JsonSerializer.Deserialize(json, TypeInfo<FilterConfig>(ContextOnlyOptions)))
            .Should().Throw<JsonException>();

    /// <summary>
    /// Every member of every enum a saved filter can carry still round-trips
    /// by name on the context path, derived from the enums rather than listed
    /// — so a member added tomorrow is covered the moment it is declared. The
    /// positive half of the strictness contract: it costs no legitimate token.
    /// </summary>
    [Fact]
    public void ContextPath_AcceptsEveryDeclaredEnumName()
    {
        var config = new FilterConfig { DecisionType = DecisionTypeOption.Both };
        foreach (var level in Enum.GetValues<AnalysisLevel>())
            config.EvaluationLevels.Add(level);
        foreach (var contact in Enum.GetValues<ContactType>())
            config.ContactTypes.Add(contact);
        foreach (var position in Enum.GetValues<PositionType>())
            config.PositionTypes.Add(position);
        foreach (var play in Enum.GetValues<PlayType>())
            config.PlayTypes.Add(play);

        foreach (var option in Enum.GetValues<DecisionTypeOption>())
        {
            config.DecisionType = option;
            var json = Serialize(config, ContextOnlyOptions);

            var restored = JsonSerializer.Deserialize(json, TypeInfo<FilterConfig>(ContextOnlyOptions))!;

            restored.Should().Be(config);
        }
    }

    /// <summary>
    /// <see cref="BoardPattern"/>'s read path still routes through
    /// <see cref="BoardPattern.Parse"/> on the context path: a malformed
    /// bracket list fails the deserialize rather than materializing an invalid
    /// pattern or silently dropping the facet.
    /// </summary>
    [Fact]
    public void ContextPath_StillRejectsAMalformedPattern() =>
        FluentActions.Invoking(
                () => JsonSerializer.Deserialize(
                    """{"PositionPattern":"[99,,0]"}""", TypeInfo<FilterConfig>(ContextOnlyOptions)))
            .Should().Throw<JsonException>();

    /// <summary>
    /// The collection envelope stays fail-loud through the context, and the
    /// entry-naming diagnostic survives — a corrupt saved filter must still
    /// name itself rather than degrading to a generic load error.
    /// </summary>
    [Fact]
    public void ContextPath_StillFailsLoudOnTheEnvelope()
    {
        var bad =
            """{"schemaVersion":1,"filters":[{"name":"Blitz","config":{"DecisionType":1}}]}""";

        FluentActions.Invoking(
                () => JsonSerializer.Deserialize(
                    bad, TypeInfo<NamedFilterCollection>(ContextOnlyOptions)))
            .Should().Throw<JsonException>()
            .WithMessage("*Blitz*");
    }

    /// <summary>
    /// The tolerant-payload half, likewise: an unknown config member is still
    /// ignored rather than failing the file, so a retired facet cannot brick a
    /// user's saved collection on the shipped path.
    /// </summary>
    [Fact]
    public void ContextPath_StillTolerartesUnknownConfigMembers()
    {
        var json =
            """{"schemaVersion":1,"filters":[{"name":"Blitz","config":{"RetiredFacet":["Ply3"],"Players":["Alice"]}}]}""";

        var restored = JsonSerializer.Deserialize(
            json, TypeInfo<NamedFilterCollection>(ContextOnlyOptions))!;

        restored.GetConfig("Blitz").Players.Should().Equal("Alice");
    }

    // -----------------------------------------------------------------------
    //  The shadow. This context's closure reaches BgDataTypes_Lib types and
    //  the generator emits its own copy of their metadata here, so a consumer
    //  chaining most-derived-first gets this copy rather than the upstream
    //  one. That is only safe while the two agree.
    // -----------------------------------------------------------------------

    [Fact]
    public void TheContext_ShadowsBgDataTypes_ButIdentically()
    {
        var shadowed = new[] { typeof(AnalysisLevel), typeof(DiceRoll) };

        // It really is a shadow: this context claims both types itself.
        foreach (var type in shadowed)
            XgFilterJsonContext.Default.GetTypeInfo(type).Should().NotBeNull(
                "the FilterConfig walk reaches {0}, so the generator emits metadata for it here", type);

        // And the shadow is invisible: same tokens either side.
        foreach (var level in Enum.GetValues<AnalysisLevel>())
        {
            JsonSerializer.Serialize(level, TypeInfo<AnalysisLevel>(ContextOnlyOptions))
                .Should().Be(JsonSerializer.Serialize(
                    level, TypeInfo<AnalysisLevel>(BgDataTypesOnlyOptions)));
        }

        foreach (var roll in AllRolls())
        {
            JsonSerializer.Serialize(roll, TypeInfo<DiceRoll>(ContextOnlyOptions))
                .Should().Be(JsonSerializer.Serialize(
                    roll, TypeInfo<DiceRoll>(BgDataTypesOnlyOptions)));
        }
    }

    private static readonly JsonSerializerOptions BgDataTypesOnlyOptions =
        new() { TypeInfoResolver = BgDataTypesJsonContext.Default };

    private static IEnumerable<DiceRoll> AllRolls()
    {
        for (int high = 1; high <= 6; high++)
            for (int low = 1; low <= high; low++)
                yield return new DiceRoll(high, low);
    }

    // -----------------------------------------------------------------------
    //  Completeness — the halheinrich/backgammon#144 intersection pattern: two
    //  independent enumerations of one fact, kept agreeing by a test.
    //
    //  Side A is this library's wire surface, derived from the assembly by the
    //  two marks that make a type a wire unit here — a type-level
    //  [JsonConverter] (the type defines its own wire token) or the
    //  ToJson/FromJson/TryFromJson persistence trio (the type is a document) —
    //  and then expanded by the serializer's own metadata graph, because a
    //  context owes metadata for everything its roots reach, not just for the
    //  roots. Side B is what the generator actually produced, read off its
    //  JsonTypeInfo<T> properties rather than off the [JsonSerializable] list.
    //
    //  Leg 3 compared roots against declarations, which worked because its two
    //  documents reached nothing. Here they reach plenty, and the generator
    //  emits a property per covered type rather than per declaration — so the
    //  comparable pair is closure against coverage. That is the stronger check
    //  in all three directions: a new wire unit lands in the closure and fails
    //  until it is covered; a type the generator silently declined (SYSLIB1030,
    //  which is what an inaccessible bundled converter produces) drops out of
    //  coverage and fails; a declaration left behind by a deleted wire type
    //  sits in coverage with nothing reaching it and fails the other way.
    // -----------------------------------------------------------------------

    [Fact]
    public void TheContextCovers_ExactlyTheClosureOfThisLibrarysWireSurface()
    {
        GeneratedCoverage().Should().Equal(Ordered(SerializedClosure()));
    }

    /// <summary>
    /// The vacuity guard on both derivations, and on each mark separately: an
    /// enumeration that silently returned nothing — or a mark that stopped
    /// matching — would satisfy the equality above. Named explicitly so the
    /// known wire units are pinned as such, and so this test, not the one
    /// above, is what fails if a derivation stops working.
    /// </summary>
    [Fact]
    public void BothDerivations_FindTheKnownWireUnits()
    {
        // The converter mark: five token-defining types plus the document
        // whose converter writes its whole envelope.
        ConverterBearing().Should().BeEquivalentTo(new[]
        {
            typeof(BoardPattern), typeof(NamedFilterCollection),
            typeof(ContactType), typeof(DecisionTypeOption),
            typeof(PlayType), typeof(PositionType),
        });

        // The trio mark: the two documents. FilterConfig is here and nowhere
        // else — it is the one wire unit no converter marks.
        TrioBearing().Should().BeEquivalentTo(new[]
        {
            typeof(FilterConfig), typeof(NamedFilterCollection),
        });

        WireSurface().Should().Contain(typeof(FilterConfig));
        WireSurface().Should().Contain(typeof(NamedFilterCollection));
        GeneratedCoverage().Should().Contain(typeof(FilterConfig));
        GeneratedCoverage().Should().Contain(typeof(NamedFilterCollection));

        // And every wire unit is a declared root, not merely a type the walk
        // happened to reach — that is what lets a chained consumer ask for one
        // by name, which ExtractFromXgToCsv's local-mode wire does for the
        // enums it carries bare.
        DeclaredRoots().Should().Equal(WireSurface());
    }

    /// <summary>
    /// And the whole serialized closure of that surface resolves through this
    /// context alone — including the BgDataTypes_Lib types a
    /// <see cref="FilterConfig"/> embeds, which the generator's walk reaches
    /// and covers here. This is what says the shipped entry points owe nothing
    /// to a resolver chain: they name a
    /// <see cref="JsonTypeInfo{T}"/> off this context and every nested
    /// resolution stays inside it. The byte-identity theories above are the
    /// same guard from the other side — a member that began resolving through
    /// a type this context does not cover would throw there while the
    /// reflection path kept working.
    /// </summary>
    [Fact]
    public void ThisReposContextAlone_ResolvesTheFullClosure()
    {
        var unresolved = SerializedClosure()
            .Where(type => XgFilterJsonContext.Default.GetTypeInfo(type) is null)
            .Select(type => type.ToString())
            .Order(StringComparer.Ordinal)
            .ToList();

        unresolved.Should().BeEmpty();
    }

    /// <summary>
    /// The closure's own vacuity guard: it must actually walk past the roots
    /// and into the members, or the check above proves nothing. Named members
    /// from three different sources — this library's enums, BgDataTypes_Lib's,
    /// and a collection spelling no <c>[JsonSerializable]</c> declares.
    /// </summary>
    [Fact]
    public void TheClosure_ReachesPastTheDeclaredRoots()
    {
        var closure = SerializedClosure();

        closure.Should().Contain(typeof(AnalysisLevel));
        closure.Should().Contain(typeof(DiceRoll));
        closure.Should().Contain(typeof(IList<AnalysisLevel>));
        closure.Should().Contain(typeof(IList<string>));
        closure.Should().Contain(typeof(double?));
        closure.Should().Contain(typeof(double));   // the nullable edge
        closure.Count.Should().BeGreaterThan(WireSurface().Count);
    }

    /// <summary>
    /// Side A, first mark: every type in this assembly carrying a type-level
    /// <c>[JsonConverter]</c>. Non-public types are included deliberately —
    /// one would be a wire unit a public context cannot declare (CS0053), and
    /// failing here is the honest way to surface that.
    /// </summary>
    private static IReadOnlyList<Type> ConverterBearing() =>
        [.. LibraryTypes()
            .Where(t => t.GetCustomAttribute<JsonConverterAttribute>(inherit: false) is not null)];

    /// <summary>
    /// Side A, second mark: every type declaring the canonical persistence
    /// trio — <c>string ToJson()</c>, <c>static T FromJson(string)</c>,
    /// <c>static bool TryFromJson(string?, out T)</c>. That trio is how this
    /// library says a type is a document, and it is the only mark
    /// <see cref="FilterConfig"/> carries: it has no converter of its own,
    /// because every member that needs one carries it instead.
    /// </summary>
    private static IReadOnlyList<Type> TrioBearing() =>
        [.. LibraryTypes().Where(HasPersistenceTrio)];

    private static bool HasPersistenceTrio(Type type)
    {
        const BindingFlags Instance = BindingFlags.Public | BindingFlags.Instance;
        const BindingFlags Static = BindingFlags.Public | BindingFlags.Static;

        if (type.GetMethod("ToJson", Instance, Type.EmptyTypes) is not { } toJson
            || toJson.ReturnType != typeof(string))
            return false;

        if (type.GetMethod("FromJson", Static, [typeof(string)]) is not { } fromJson
            || fromJson.ReturnType != type)
            return false;

        return type.GetMethod("TryFromJson", Static, [typeof(string), type.MakeByRefType()])
            is { } tryFromJson && tryFromJson.ReturnType == typeof(bool);
    }

    /// <summary>Side A: the union of the two marks, in a stable order.</summary>
    private static IReadOnlyList<Type> WireSurface() =>
        Ordered(ConverterBearing().Union(TrioBearing()));

    /// <summary>
    /// Side B: every type the generator actually produced metadata for, read
    /// off the context's own <see cref="JsonTypeInfo{T}"/> properties rather
    /// than off its <c>[JsonSerializable]</c> attributes — so a type the
    /// generator silently declined (SYSLIB1030, which is what an inaccessible
    /// bundled converter produces) counts as absent here, which is what it is
    /// at runtime. The generator emits one such property per covered type, not
    /// only per declaration, which is why this is the counterpart of the
    /// closure rather than of the root list.
    /// </summary>
    private static IReadOnlyList<Type> GeneratedCoverage() =>
        Ordered(typeof(XgFilterJsonContext)
            .GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Select(p => p.PropertyType)
            .Where(t => t.IsGenericType && t.GetGenericTypeDefinition() == typeof(JsonTypeInfo<>))
            .Select(t => t.GetGenericArguments()[0]));

    /// <summary>
    /// The context's declared roots, read off its <c>[JsonSerializable]</c>
    /// attributes. Deliberately the <em>stated</em> list rather than the
    /// generated one: its job is to say what this library publishes as a named
    /// wire unit, which <see cref="GeneratedCoverage"/> cannot distinguish
    /// from what the property walk merely reached.
    /// </summary>
    private static IReadOnlyList<Type> DeclaredRoots() =>
        Ordered(typeof(XgFilterJsonContext).GetCustomAttributesData()
            .Where(a => a.AttributeType == typeof(JsonSerializableAttribute))
            .Select(a => (Type)a.ConstructorArguments[0].Value!));

    private static IReadOnlyList<Type> Ordered(IEnumerable<Type> types) =>
        [.. types.OrderBy(t => t.ToString(), StringComparer.Ordinal)];

    private static IEnumerable<Type> LibraryTypes() =>
        typeof(FilterConfig).Assembly.GetTypes();

    /// <summary>
    /// The closure of <see cref="WireSurface"/> under the serializer's own
    /// metadata graph. Asks the serializer what each type serializes as rather
    /// than re-deriving it by reflection; kind <see cref="JsonTypeInfoKind.None"/>
    /// means a converter owns the wire form wholesale and the serializer never
    /// walks it, so neither does this.
    /// </summary>
    private static HashSet<Type> SerializedClosure()
    {
        var closure = new HashSet<Type>();
        var pending = new Queue<Type>(WireSurface());
        while (pending.Count > 0)
        {
            var type = pending.Dequeue();
            if (!closure.Add(type))
                continue;

            if (!ContextOnlyOptions.TryGetTypeInfo(type, out var info))
                continue;

            // A Nullable<T> resolves its underlying T in its own right — an
            // edge of the metadata graph that JsonTypeInfoKind does not
            // expose, and the only member shape here that has one.
            if (Nullable.GetUnderlyingType(type) is { } underlying)
                pending.Enqueue(underlying);

            switch (info.Kind)
            {
                case JsonTypeInfoKind.Object:
                    foreach (var property in info.Properties)
                        pending.Enqueue(property.PropertyType);
                    break;
                case JsonTypeInfoKind.Enumerable:
                case JsonTypeInfoKind.Dictionary:
                    if (info.ElementType is not null)
                        pending.Enqueue(info.ElementType);
                    break;
            }
        }

        return closure;
    }

    // -----------------------------------------------------------------------
    //  Posture — the declarations that make the arc's rules gates rather than
    //  suggestions. Asserted here so flipping any of them off in the csproj,
    //  the context, or a converter's accessibility fails a test rather than
    //  silently reopening the reflection path, the fast-path capture, or the
    //  downstream metadata hole.
    // -----------------------------------------------------------------------

    [Fact]
    public void TheLibraryAssembly_DeclaresItselfTrimmable()
    {
        typeof(FilterConfig).Assembly.GetCustomAttributes<AssemblyMetadataAttribute>()
            .Should().Contain(a => a.Key == "IsTrimmable" && a.Value == "True");
    }

    /// <summary>
    /// Metadata-only generation, the arc's rule (2): a default-mode fast-path
    /// handler binds nested type resolution to the declaring context's own
    /// private options and bypasses the resolver chain. BgDataTypes_Lib's
    /// chained-consumer test pair demonstrates that failure and owns the rule;
    /// this is the declaration pin that keeps this link of the chain honest.
    /// </summary>
    [Fact]
    public void TheContext_GeneratesMetadataOnly()
    {
        var options = typeof(XgFilterJsonContext)
            .GetCustomAttribute<JsonSourceGenerationOptionsAttribute>();

        options.Should().NotBeNull();
        options!.GenerationMode.Should().Be(JsonSourceGenerationMode.Metadata);
    }

    /// <summary>
    /// The arc's rule (1), and the one this leg had to measure rather than
    /// inherit: every type-level converter this library bundles must be
    /// public. Not for our own context — the generator runs in this assembly,
    /// where <c>internal</c> resolves fine — but for a <em>consumer's</em>.
    /// ExtractFromXgToCsv's <c>ProcessRequest</c> carries a
    /// <see cref="FilterConfig"/>, so its generator walks into
    /// <see cref="BoardPattern"/>; with an internal converter it emits
    /// SYSLIB1220 then SYSLIB1030 and drops the type, leaving a metadata hole
    /// that only bites once trimming removes the reflection fallback. Both
    /// diagnostics are warnings, so nothing downstream would have failed — the
    /// gate has to live here.
    /// </summary>
    [Fact]
    public void EveryBundledConverter_IsPubliclyConstructible()
    {
        var converters = LibraryTypes()
            .Select(t => t.GetCustomAttribute<JsonConverterAttribute>(inherit: false)?.ConverterType)
            .Where(t => t is not null)
            .Distinct()
            .ToList();

        converters.Should().NotBeEmpty();
        converters.Should().AllSatisfy(converter =>
        {
            converter!.IsPublic.Should().BeTrue(
                "{0} is bundled by attribute, so a consumer's source generator must be able to " +
                "construct it", converter);
            converter.GetConstructor(Type.EmptyTypes).Should().NotBeNull(
                "{0} must have a public parameterless constructor for attribute-form registration",
                converter);
        });
    }

    private static string Serialize<T>(T value, JsonSerializerOptions options) =>
        JsonSerializer.Serialize(value, TypeInfo<T>(options));

    private static JsonTypeInfo<T> TypeInfo<T>(JsonSerializerOptions options)
        => (JsonTypeInfo<T>)options.GetTypeInfo(typeof(T));
}
