using BgDataTypes_Lib;

namespace XgFilter_Lib.Filtering;

/// <summary>
/// The saved-filters document's wire identity: the closed
/// <see cref="NamedCollectionJsonConverter{TValue, TSelf}"/> for
/// <see cref="NamedFilterCollection"/>, supplying the two property names that
/// are this document's own — <c>filters</c> for the entries array and
/// <c>config</c> for each entry's body. The envelope itself (schema version,
/// structure, the name rule, the duplicate check, entry-order tolerance, and
/// the delegation of each body to <see cref="FilterConfig"/>'s
/// <see cref="IJsonDocument{TSelf}"/> seam) is the base's and is documented
/// there. Bundled by the type-level <c>[JsonConverter]</c> on the collection,
/// so consumers register nothing.
///
/// <para>
/// <b>Those two names are a file contract, not a detail.</b> Every
/// <c>xg-filters.json</c> already on a user's disk spells them <c>filters</c>
/// and <c>config</c>; changing either is a file migration rather than a
/// refactor, and <c>NamedFilterCollectionSerializationTests</c> pins the
/// resulting bytes exactly.
/// </para>
///
/// <para>
/// <b>Sealed, public, parameterless — because the source generator needs it
/// to be</b> (halheinrich/backgammon#129 leg 4, and the rule every bundled
/// converter in this library states). An open generic cannot be named by an
/// attribute and the base has no parameterless constructor by design, so the
/// closed type is what makes the pattern work at all. The generator emits
/// <c>new NamedFilterCollectionJsonConverter()</c> into the <em>declaring</em>
/// assembly of every context naming the document, so an internal converter
/// fails a consumer's context with SYSLIB1220 then SYSLIB1030 and silently
/// drops the type — measured on net10.0 / SDK 10.0.400. A converter factory
/// activating a closed converter at runtime is the reflection path this
/// pattern exists to avoid, and the trim analyzer here rejects it.
/// </para>
/// </summary>
public sealed class NamedFilterCollectionJsonConverter
    : NamedCollectionJsonConverter<FilterConfig, NamedFilterCollection>
{
    /// <summary>
    /// Initializes the converter with this document's two wire names.
    /// </summary>
    public NamedFilterCollectionJsonConverter() : base("filters", "config") { }
}
