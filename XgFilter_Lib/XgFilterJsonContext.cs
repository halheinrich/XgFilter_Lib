using System.Text.Json.Serialization;
using XgFilter_Lib.Enums;
using XgFilter_Lib.Filtering;
using XgFilter_Lib.Patterns;

namespace XgFilter_Lib;

/// <summary>
/// The source-generated <see cref="JsonSerializerContext"/> for this
/// library's wire surface — trim-safe <c>System.Text.Json</c> metadata for
/// every type this library puts on a wire, produced at compile time instead
/// of by runtime reflection (halheinrich/backgammon#129 leg 4). The
/// mechanism changes, the bytes do not: serialization through this context
/// is byte-identical to the reflection path, pinned by test, and every
/// bundled <c>[JsonConverter]</c> — the four
/// <see cref="StrictJsonStringEnumConverter{TEnum}"/> enums,
/// <see cref="BoardPatternJsonConverter"/>,
/// <see cref="NamedFilterCollectionJsonConverter"/> — is honored unchanged.
///
/// <para>
/// <b>What is declared, and why.</b> The <c>[JsonSerializable]</c> roots are
/// this library's wire units, and they arrive by two independent routes that
/// <c>XgFilterJsonContextTests</c> derives from the assembly and intersects:
/// <list type="bullet">
///   <item><description>
///     <b>The persistence trio.</b> <see cref="FilterConfig"/> and
///     <see cref="NamedFilterCollection"/> each own a
///     <c>ToJson</c> / <c>FromJson</c> / <c>TryFromJson</c> trio, which is
///     how this library says "this type is a document." Those six call sites
///     are every place this assembly touches
///     <see cref="System.Text.Json.JsonSerializer"/>, and all six now name a
///     <see cref="System.Text.Json.Serialization.Metadata.JsonTypeInfo{T}"/>
///     off this context.
///   </description></item>
///   <item><description>
///     <b>A bundled type-level <c>[JsonConverter]</c>.</b>
///     <see cref="BoardPattern"/>, <see cref="ContactType"/>,
///     <see cref="DecisionTypeOption"/>, <see cref="PlayType"/> and
///     <see cref="PositionType"/> define their own wire token, which is what
///     makes each a wire unit in its own right rather than an implementation
///     detail of the config that holds it. They ride the generator's
///     property-graph walk from <see cref="FilterConfig"/> as well, but they
///     are declared because a chained consumer resolves them
///     <em>by name</em>: ExtractFromXgToCsv's local-mode wire crosses these
///     enums as bare members of its own request shape, which is the whole
///     point of halheinrich/backgammon#37's bundling.
///   </description></item>
/// </list>
/// <see cref="NamedFilterCollection"/> is a member of both sets. Nothing
/// else in this assembly reaches a serializer.
/// </para>
///
/// <para>
/// <b>Public, deliberately</b> — the arc's standing shape
/// (<c>BgDataTypesJsonContext</c> is the precedent), and here it is
/// load-bearing rather than conventional. The two consumers differ, and the
/// measurement is what decides it:
/// <list type="bullet">
///   <item><description>
///     XgFilter_Razor's <c>SavedFiltersStore</c> — and BgQuiz behind it —
///     never names these types to a serializer; it round-trips through
///     <see cref="NamedFilterCollection.ToJson"/> /
///     <see cref="NamedFilterCollection.TryFromJson"/> and owns no
///     <c>JsonSerializerOptions</c> at all. That consumer alone would have
///     admitted an <see langword="internal"/> context, leg 2's shape.
///   </description></item>
///   <item><description>
///     ExtractFromXgToCsv does not. Its <c>ProcessRequest</c> carries a
///     <c>public FilterConfig Filters</c>, the client POSTs it with
///     <c>PostAsJsonAsync</c> and the server binds it as
///     <c>[FromBody]</c> under a bare <c>AddControllers()</c> — so
///     <see cref="FilterConfig"/> is named to two serializers this library
///     does not own. A consumer in that position needs metadata it can
///     reach.
///   </description></item>
/// </list>
/// It composes by chaining type-info resolvers — no consumer-side converter
/// registration, no glue types:
/// <code>
/// var options = new JsonSerializerOptions
/// {
///     TypeInfoResolver = JsonTypeInfoResolver.Combine(
///         TheConsumersOwnContext.Default,
///         XgFilterJsonContext.Default,
///         BgDataTypesJsonContext.Default)
/// };
/// </code>
/// (equivalently, add each context to
/// <c>JsonSerializerOptions.TypeInfoResolverChain</c>). The chain is
/// searched in order, first resolver claiming a type wins — order contexts
/// most-derived-first.
/// </para>
///
/// <para>
/// <b>This context chains nothing, and it shadows BgDataTypes_Lib on
/// purpose.</b> Unlike leg 3, whose closure genuinely stopped at its own
/// roots, a <see cref="FilterConfig"/> embeds BgDataTypes_Lib types: three
/// <c>IList&lt;AnalysisLevel&gt;</c> depth facets and an
/// <c>IList&lt;DiceRoll&gt;</c>, each carrying that library's own bundled
/// converter. The generator's property-graph walk reaches them and emits
/// metadata for them <em>in this assembly</em> — measured: this context
/// resolves <c>AnalysisLevel</c>, <c>DiceRoll</c> and every collection
/// spelling in the closure on its own, so the shipped entry points need no
/// chain and there is no options object here for one to hang on. What that
/// costs is a shadow: a consumer chaining
/// <c>Combine(XgFilterJsonContext.Default, BgDataTypesJsonContext.Default)</c>
/// gets <em>this</em> copy of those two types, not the upstream one. It is
/// benign because both copies are generated from the same type-level
/// attribute, and <c>XgFilterJsonContextTests</c> refuses to take that on
/// faith — it pins the two copies byte-identical, so a divergence upstream
/// fails here rather than in a consumer that happened to order the chain
/// this way.
/// </para>
///
/// <para>
/// <b>Metadata-only generation, per the arc's binding rule.</b> The default
/// generation mode also emits fast-path serialize handlers, and a fast-path
/// handler binds every nested type resolution to the <em>declaring
/// context's own private options</em>, not the runtime options it was
/// invoked with — silently bypassing the resolver chain. With
/// <see cref="JsonSourceGenerationMode.Metadata"/> on every context in the
/// chain there is no context-private options capture: resolution always
/// flows through the combined options. BgDataTypes_Lib's chained-consumer
/// test pair demonstrates both the failure and the working shape and owns
/// the rule; every downstream context declares the same mode.
/// </para>
///
/// <para>
/// <b>No options-level registrations exist to express.</b> Every converter
/// this library needs is bundled by type-level <c>[JsonConverter]</c>, and
/// the canonical options this context replaces registered nothing at all —
/// halheinrich/backgammon#16's one genuinely redundant registration was
/// already deduped in the halheinrich/backgammon#37 sweep, precisely so the
/// attributes and not an options object would govern every read. So this
/// context's own options carry nothing beyond the generation mode, and
/// serializing through them is the supported path rather than leg 2's trap:
/// there is no naming policy, no formatting and no converter list for the
/// attribute to have failed to mirror. That is what lets the shipped entry
/// points name <c>Default.FilterConfig</c> and
/// <c>Default.NamedFilterCollection</c> directly instead of carrying an
/// options object beside them.
/// </para>
/// </summary>
[JsonSourceGenerationOptions(GenerationMode = JsonSourceGenerationMode.Metadata)]
[JsonSerializable(typeof(FilterConfig))]
[JsonSerializable(typeof(NamedFilterCollection))]
[JsonSerializable(typeof(BoardPattern))]
[JsonSerializable(typeof(ContactType))]
[JsonSerializable(typeof(DecisionTypeOption))]
[JsonSerializable(typeof(PlayType))]
[JsonSerializable(typeof(PositionType))]
public sealed partial class XgFilterJsonContext : JsonSerializerContext
{
}
