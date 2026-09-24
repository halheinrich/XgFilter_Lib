using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;
using BgDataTypes_Lib;

namespace XgFilter_Lib.Filtering;

/// <summary>
/// The versioned saved-filters document: an immutable collection of named
/// <see cref="FilterConfig"/> entries — the pick list a consumer offers when
/// the user saves and reloads filter configurations, persisted as
/// <c>xg-filters.json</c> beside the consumer's own state (BgQuiz keeps one
/// per directory). This library does no I/O; consumers load bytes, restore,
/// apply withers, and write back.
///
/// <para>
/// <b>A specialization, owning only its identity.</b> Everything structural
/// belongs to <see cref="NamedCollection{TValue, TSelf}"/> in BgDataTypes_Lib
/// and is documented there: the name rule (<c>OrdinalIgnoreCase</c>, the
/// single definition of "same name"), the canonical name-sorted order, the
/// snapshot contract that stores each config's serialized value rather than
/// the caller's instance, the strict-envelope/tolerant-payload split, the
/// <see cref="IJsonDocument{TSelf}"/> trio, and reference equality. The whole
/// of what this type contributes is the three things the generic machinery
/// cannot know — how to construct one, where the serializer finds its
/// metadata (<see cref="XgFilterJsonContext"/>), and its wire vocabulary,
/// which lives on <see cref="NamedFilterCollectionJsonConverter"/>. Read
/// BgDataTypes_Lib's <c>INSTRUCTIONS.md</c> Pitfalls entry on the pattern
/// before changing any part of it: every part is load-bearing
/// (halheinrich/backgammon#190 leg (B)).
/// </para>
///
/// <para>
/// <b>The payload is what makes this document worth having.</b>
/// <see cref="FilterConfig"/>'s own <see cref="IJsonDocument{TSelf}"/> trio is
/// both the snapshot mechanism and the entry-body seam, and its tolerance of
/// absent members is what the base's tolerant-payload half delegates to — so a
/// retired config facet never bricks a user's saved collection. Because a
/// stored config is restored rather than re-entered, this is also where
/// <see cref="FilterConfig.GetInvalidFields"/> earns its posture: a document
/// written before a rule existed loads intact and reports invalid at apply,
/// instead of failing the restore and losing the value the user has to fix.
/// Position patterns are the exception: <see cref="FilterConfig.PositionPattern"/>
/// is typed, so a stored pattern a newer rule refuses fails its entry, and the
/// strict envelope then fails the whole file (tracked as
/// halheinrich/backgammon#269).
/// </para>
/// </summary>
[JsonConverter(typeof(NamedFilterCollectionJsonConverter))]
public sealed class NamedFilterCollection
    : NamedCollection<FilterConfig, NamedFilterCollection>,
      INamedCollectionSpecialization<FilterConfig, NamedFilterCollection>
{
    private NamedFilterCollection(Entries entries) : base(entries) { }

    /// <summary>
    /// The base class's one construction path into this type. The
    /// <see cref="NamedCollection{TValue, TSelf}.Entries"/> argument is
    /// constructible only inside BgDataTypes_Lib, so this is not a hatch past
    /// the name rule or the snapshot contract: nothing outside the machinery
    /// can call it with anything.
    /// </summary>
    static NamedFilterCollection INamedCollectionSpecialization<FilterConfig, NamedFilterCollection>
        .Create(Entries entries) => new(entries);

    /// <summary>
    /// Where the base's trio reaches a serializer: this document's
    /// source-generated metadata off <see cref="XgFilterJsonContext"/>, where
    /// it is a declared <c>[JsonSerializable]</c> root
    /// (halheinrich/backgammon#129 leg 4). The metadata carries no policy of
    /// its own — <see cref="NamedFilterCollectionJsonConverter"/> writes and
    /// reads the whole envelope by hand — and naming it here rather than a
    /// reflection-bound overload is what keeps the document trim-safe in a
    /// consumer that never names this type.
    /// </summary>
    static JsonTypeInfo<NamedFilterCollection> INamedCollectionSpecialization<FilterConfig, NamedFilterCollection>
        .CanonicalTypeInfo => XgFilterJsonContext.Default.NamedFilterCollection;
}
