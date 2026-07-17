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
/// <see cref="JsonSerializerOptions"/> (<c>FilterConfig</c>'s canonical options
/// deliberately omit it): the type-level <see cref="JsonConverterAttribute"/>
/// on <see cref="BoardPattern"/> is the single wiring point, and it reaches
/// this converter regardless of accessibility — System.Text.Json instantiates
/// the attribute-named type via reflection, so <c>internal</c> works on the
/// real wire path (pinned by <c>BoardPatternWireSafetyTests</c>).
/// </summary>
internal sealed class BoardPatternJsonConverter : JsonConverter<BoardPattern>
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
