using System.Text.Json.Serialization;

namespace XgFilter_Lib.Enums;

/// <summary>
/// The string-token-exact enum converter: a
/// <see cref="JsonStringEnumConverter{TEnum}"/> that rejects numeric tokens on
/// read (and refuses to write an undefined value as a number), for
/// attribute-form registration — where the base type's
/// <c>allowIntegerValues: false</c> knob is otherwise unreachable, because an
/// attribute can only name a converter type, not pass it constructor arguments.
///
/// <para>Bundling this onto the enum itself, rather than relying on a
/// registration in some particular <see cref="System.Text.Json.JsonSerializerOptions"/>,
/// is what makes the guarantee travel. <c>FilterConfig</c>'s own canonical
/// options already read these enums strictly, but a <c>FilterConfig</c> also
/// crosses wires this library does not own — ExtractFromXgToCsv POSTs one to its
/// local server — and on those wires the enums previously crossed as bare
/// integer ordinals, because nothing had told the serializer otherwise. A type
/// attribute is the only form that reaches a consumer's serializer
/// (halheinrich/backgammon#164, halheinrich/backgammon#37).</para>
///
/// <para>Deliberately no naming policy: the declared name <i>is</i> the wire
/// token for these types — it is what every saved filter already on disk
/// spells — and applying a policy here would silently rewrite all of them. Name
/// matching on read stays case-insensitive, the base converter's behavior, which
/// has no knob to change; the strictness closed here is token kind, not case.</para>
///
/// <para>Note the precedence rule this interacts with: an options-level
/// converter <i>outranks</i> a type attribute. So this attribute is the floor a
/// consumer gets for free, not a ceiling it cannot lower — a consumer that
/// registers a loose <see cref="JsonStringEnumConverter"/> on its own options
/// still defeats it. Pinned in <c>EnumTokenStrictnessTests</c>.</para>
/// </summary>
/// <typeparam name="TEnum">The enum type the converter handles.</typeparam>
public sealed class StrictJsonStringEnumConverter<TEnum> : JsonStringEnumConverter<TEnum>
    where TEnum : struct, Enum
{
    /// <summary>Creates the converter; attribute-form registration uses this.</summary>
    public StrictJsonStringEnumConverter()
        : base(namingPolicy: null, allowIntegerValues: false)
    {
    }
}
