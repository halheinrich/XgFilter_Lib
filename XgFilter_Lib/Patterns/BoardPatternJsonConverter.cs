using System.Text.Json;
using System.Text.Json.Serialization;

namespace XgFilter_Lib.Patterns;

/// <summary>
/// Serializes a <see cref="BoardPattern"/> as its bracket-list string and
/// reads it back through <see cref="BoardPattern.Parse"/>, so the wire form is
/// the same human-readable text a consumer types. Parsing through
/// <see cref="BoardPattern.Parse"/> keeps deserialization on the validated path
/// — a malformed or out-of-range pattern in the JSON fails fast rather than
/// materializing an invalid object. Never registered on any
/// <see cref="JsonSerializerOptions"/>: the type-level
/// <see cref="JsonConverterAttribute"/> on <see cref="BoardPattern"/> is the
/// single wiring point (pinned by <c>BoardPatternWireSafetyTests</c>).
///
/// <para>
/// <b>Public because the source generator needs it to be</b>
/// (halheinrich/backgammon#129 leg 4). On the reflection path accessibility
/// is irrelevant — System.Text.Json instantiates the attribute-named type
/// itself, and <c>internal</c> worked. The source generator emits
/// <c>new BoardPatternJsonConverter()</c> into the <em>declaring</em>
/// assembly instead, so a consumer's context that walks into
/// <see cref="BoardPattern"/> — ExtractFromXgToCsv's <c>ProcessRequest</c>
/// carries a <c>FilterConfig</c>, and the walk reaches this type from its
/// pattern facet — cannot construct it. Measured on net10.0 / SDK 10.0.400:
/// the generator reports SYSLIB1220 (no accessible parameterless
/// constructor) and then SYSLIB1030 (did not generate serialization
/// metadata) and <em>drops the type</em>, leaving a hole in the consumer's
/// metadata that only surfaces once trimming removes the reflection
/// fallback. Warnings, not errors — which is why this is a rule of the arc
/// rather than something a build would have told us.
/// </para>
/// </summary>
public sealed class BoardPatternJsonConverter : JsonConverter<BoardPattern>
{
    /// <inheritdoc/>
    public override BoardPattern? Read(
        ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType == JsonTokenType.Null)
            return null;

        if (reader.TokenType != JsonTokenType.String)
            throw new JsonException(
                $"Expected a string bracket-list for {nameof(BoardPattern)}, got {reader.TokenType}.");

        var text = reader.GetString()!;

        try
        {
            return BoardPattern.Parse(text);
        }
        catch (Exception ex) when (ex is FormatException or ArgumentException)
        {
            throw new JsonException(
                $"Invalid {nameof(BoardPattern)} bracket-list '{text}'.", ex);
        }
    }

    /// <inheritdoc/>
    public override void Write(
        Utf8JsonWriter writer, BoardPattern value, JsonSerializerOptions options) =>
        writer.WriteStringValue(value.ToBracketList());
}
