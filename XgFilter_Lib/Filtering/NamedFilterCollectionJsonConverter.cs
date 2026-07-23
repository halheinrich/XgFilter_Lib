using System.Text.Json;
using System.Text.Json.Serialization;

namespace XgFilter_Lib.Filtering;

/// <summary>
/// Serialises <see cref="NamedFilterCollection"/> as its versioned persistent
/// wire format. Bundled via type-level <c>[JsonConverter]</c> on the
/// collection (the same pattern as <see cref="Patterns.BoardPatternJsonConverter"/> and
/// BgGame_Lib's document converters), so consumers do not need to register
/// anything on their <see cref="JsonSerializerOptions"/>.
///
/// <para>
/// The envelope is hand-written with fixed property names — the persisted
/// format is a file contract and must not vary with the consumer's options
/// (naming policy etc.). Whitespace is the one thing options still control
/// (<see cref="JsonSerializerOptions.WriteIndented"/> lives on the writer the
/// serializer creates), so byte-stable files additionally need fixed
/// consumer-side options. Writes order entries by name — the document's
/// canonical order — so a given collection always serializes to the same
/// content. Wire shape (schema version
/// <see cref="NamedFilterCollection.CurrentSchemaVersion"/>):
/// </para>
///
/// <code>
/// {
///   "schemaVersion": 1,
///   "filters": [
///     { "name": "Blitz mistakes", "config": { ...FilterConfig canonical JSON... } }
///   ]
/// }
/// </code>
///
/// <para>
/// <b>The envelope is strict and fail-loud.</b> A schema version other than
/// <see cref="NamedFilterCollection.CurrentSchemaVersion"/> (with a
/// distinguished "newer than this library supports" message — a version bump
/// is the envelope's only evolution mechanism), a missing required property,
/// an unknown property at the top or entry level, an invalid name (blank or
/// untrimmed — the same single-sourced rule as
/// <see cref="NamedFilterCollection.With"/>, which reads route through), or a
/// duplicate name per the document's name rule (case-insensitive; checked
/// explicitly here because <see cref="NamedFilterCollection.With"/> would
/// silently replace) all throw <see cref="JsonException"/>. Entry order is
/// the one envelope liberty: reads accept any order and re-canonicalize,
/// because order is presentation, not semantics.
/// </para>
///
/// <para>
/// <b>Config bodies are tolerant payload.</b> Each entry's <c>config</c> body
/// is handed verbatim to <see cref="FilterConfig.FromJson"/> — the canonical
/// deserialization seam — so unknown or retired members are ignored exactly
/// as <see cref="FilterConfig"/> itself ignores them, and a facet retirement
/// never bricks a saved collection. A body that
/// <see cref="FilterConfig"/> itself rejects (invalid enum name, malformed
/// pattern, the <c>null</c> token) is corruption, not evolution, and fails
/// the whole file with the entry named in the message — a silently-reset
/// saved filter would filter nothing, which is worse than a loud error.
/// <see cref="NamedFilterCollection.TryFromJson"/> is the tolerant restore
/// path.
/// </para>
/// </summary>
internal sealed class NamedFilterCollectionJsonConverter : JsonConverter<NamedFilterCollection>
{
    public override NamedFilterCollection? Read(
        ref Utf8JsonReader reader,
        Type typeToConvert,
        JsonSerializerOptions options)
    {
        if (reader.TokenType == JsonTokenType.Null)
            return null;

        if (reader.TokenType != JsonTokenType.StartObject)
            throw new JsonException(
                $"Expected object for NamedFilterCollection, got {reader.TokenType}.");

        int? schemaVersion = null;
        List<(string Name, FilterConfig Config)>? filters = null;

        while (reader.Read() && reader.TokenType != JsonTokenType.EndObject)
        {
            var name = reader.GetString();
            reader.Read();
            switch (name)
            {
                case "schemaVersion":
                    schemaVersion = ReadSchemaVersion(ref reader);
                    break;
                case "filters":
                    filters = ReadFilters(ref reader);
                    break;
                default:
                    throw new JsonException(
                        $"Unknown NamedFilterCollection property '{name}'.");
            }
        }

        if (schemaVersion is null)
            throw new JsonException("Missing required property 'schemaVersion'.");
        if (filters is null)
            throw new JsonException("Missing required property 'filters'.");

        var collection = NamedFilterCollection.Empty;
        var seen = new HashSet<string>(NamedFilterCollection.NameComparer);
        foreach (var (filterName, config) in filters)
        {
            if (!seen.Add(filterName))
                throw new JsonException($"Duplicate filter name '{filterName}'.");

            try
            {
                // Routes through With so the wire-level name rule has the same
                // single definition as the in-memory one. With re-snapshots the
                // freshly parsed config — a second round-trip per entry, seen
                // and accepted: one construction path, stored form guaranteed
                // canonical, negligible at load-a-pick-list scale.
                collection = collection.With(filterName, config);
            }
            catch (ArgumentException ex)
            {
                throw new JsonException(ex.Message, ex);
            }
        }

        return collection;
    }

    private static int ReadSchemaVersion(ref Utf8JsonReader reader)
    {
        if (reader.TokenType != JsonTokenType.Number || !reader.TryGetInt32(out int version))
            throw new JsonException(
                $"Expected integer for 'schemaVersion', got {reader.TokenType}.");

        if (version > NamedFilterCollection.CurrentSchemaVersion)
            throw new JsonException(
                $"Saved-filter collection has schema version {version}, newer than the highest " +
                $"version this library supports ({NamedFilterCollection.CurrentSchemaVersion}).");
        if (version != NamedFilterCollection.CurrentSchemaVersion)
            throw new JsonException(
                $"Saved-filter collection has unsupported schema version {version}; " +
                $"expected {NamedFilterCollection.CurrentSchemaVersion}.");

        return version;
    }

    private static List<(string Name, FilterConfig Config)> ReadFilters(ref Utf8JsonReader reader)
    {
        if (reader.TokenType != JsonTokenType.StartArray)
            throw new JsonException($"Expected array for 'filters', got {reader.TokenType}.");

        var filters = new List<(string, FilterConfig)>();
        while (reader.Read() && reader.TokenType != JsonTokenType.EndArray)
            filters.Add(ReadFilter(ref reader));

        return filters;
    }

    private static (string Name, FilterConfig Config) ReadFilter(ref Utf8JsonReader reader)
    {
        if (reader.TokenType != JsonTokenType.StartObject)
            throw new JsonException(
                $"Expected object for a filters element, got {reader.TokenType}.");

        string? name = null;
        JsonDocument? configBody = null;

        try
        {
            while (reader.Read() && reader.TokenType != JsonTokenType.EndObject)
            {
                var property = reader.GetString();
                reader.Read();
                switch (property)
                {
                    case "name":
                        if (reader.TokenType != JsonTokenType.String)
                            throw new JsonException(
                                $"Expected string for 'name', got {reader.TokenType}.");
                        name = reader.GetString();
                        break;
                    case "config":
                        // Captured raw and re-parsed below through the
                        // FilterConfig seam — the tolerant-payload boundary.
                        configBody = JsonDocument.ParseValue(ref reader);
                        break;
                    default:
                        throw new JsonException(
                            $"Unknown filters-element property '{property}'.");
                }
            }

            if (name is null)
                throw new JsonException(
                    "Filters element is missing required property 'name'.");
            if (configBody is null)
                throw new JsonException(
                    $"Saved filter '{name}' is missing required property 'config'.");

            try
            {
                return (name, FilterConfig.FromJson(configBody.RootElement.GetRawText()));
            }
            catch (Exception ex) when (ex is JsonException or ArgumentException)
            {
                // Fail the file, naming the entry: a body FilterConfig rejects
                // is corruption, not evolution (unknown members were already
                // absorbed by the seam above).
                throw new JsonException(
                    $"Saved filter '{name}' has an invalid config body: {ex.Message}", ex);
            }
        }
        finally
        {
            configBody?.Dispose();
        }
    }

    public override void Write(
        Utf8JsonWriter writer,
        NamedFilterCollection value,
        JsonSerializerOptions options)
    {
        writer.WriteStartObject();
        writer.WriteNumber("schemaVersion", NamedFilterCollection.CurrentSchemaVersion);
        writer.WriteStartArray("filters");

        foreach (var (name, config) in value.CanonicalEntries)   // name-sorted — canonical
        {
            writer.WriteStartObject();
            writer.WriteString("name", name);
            writer.WritePropertyName("config");

            // Embedded via the canonical serializer so FilterConfig.ToJson
            // stays the single definition of a config's wire form.
            using (var body = JsonDocument.Parse(config.ToJson()))
                body.RootElement.WriteTo(writer);

            writer.WriteEndObject();
        }

        writer.WriteEndArray();
        writer.WriteEndObject();
    }
}
