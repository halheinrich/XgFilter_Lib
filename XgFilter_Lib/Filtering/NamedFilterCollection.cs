using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;

namespace XgFilter_Lib.Filtering;

/// <summary>
/// The versioned saved-filters document: an immutable collection of named
/// <see cref="FilterConfig"/> entries — the pick list a consumer offers when
/// the user saves and reloads filter configurations (e.g. BgQuiz's
/// per-directory <c>bgquiz-filters.json</c>, kept beside its stats file). The
/// library does no I/O; consumers load bytes, deserialize, apply withers, and
/// serialize back.
///
/// <para>
/// <b>The name rule.</b> One comparer
/// (<see cref="StringComparer.OrdinalIgnoreCase"/>) is the single definition
/// of "same name" everywhere it could matter: duplicate rejection,
/// <see cref="Contains"/>, <see cref="GetConfig"/> /
/// <see cref="TryGetConfig"/> lookup, replace-on-<see cref="With"/>,
/// <see cref="Without"/>, and the canonical sort — which the rule makes total,
/// since case-variant duplicates cannot coexist. Display case is preserved as
/// typed; a replacing <see cref="With"/> stores the new spelling (last write
/// wins for name and config alike). Names are validated, never coerced:
/// blank or untrimmed names are rejected, in memory and on the wire.
/// </para>
///
/// <para>
/// <b>One canonical order.</b> Entries sort by name — in <see cref="Names"/>
/// and on the wire — so a given collection always serializes to the same
/// content regardless of the add/remove sequence that built it. Reads accept
/// entries in any order and re-canonicalize: order is presentation, not
/// semantics, so a hand-reordered file is not corruption (the duplicate check
/// still gates).
/// </para>
///
/// <para>
/// <b>Snapshot contract.</b> <see cref="FilterConfig"/> is deliberately
/// mutable (UI state binds to it), so this document stores each config's
/// <i>serialized value</i>, never the caller's instance: <see cref="With"/>
/// snapshots on the way in via the canonical
/// <see cref="FilterConfig.ToJson"/> / <see cref="FilterConfig.FromJson"/>
/// round-trip — which also normalizes, so the stored value is exactly what
/// the wire will carry — and every retrieval hands out a fresh snapshot on
/// the way out. Mutating a config after saving it, or mutating one retrieved
/// from the document, never affects the document; saving an edit back is an
/// explicit <see cref="With"/>.
/// </para>
///
/// <para>
/// <b>Wire format: strict envelope, tolerant payload.</b> JSON via the
/// bundled internal <see cref="NamedFilterCollectionJsonConverter"/>
/// (type-level <c>[JsonConverter]</c> — consumers register nothing) with
/// schema version <see cref="CurrentSchemaVersion"/>. The envelope (version,
/// structure, names) is fail-loud, with a version bump as its only evolution
/// mechanism; entry config bodies delegate to <see cref="FilterConfig"/>'s
/// own deserialization, which ignores unknown members — a retired config
/// facet must never brick a user's saved collection. Consumers round-trip
/// through <see cref="ToJson"/> / <see cref="FromJson"/>, with
/// <see cref="TryFromJson"/> as the absent-or-corrupt-restores-to-empty path
/// — the <see cref="FilterConfig"/> persistence trio.
/// </para>
///
/// <para>
/// A plain class, not a record: it wraps a collection, where record equality
/// would silently be reference equality. Instances compare by reference.
/// </para>
/// </summary>
[JsonConverter(typeof(NamedFilterCollectionJsonConverter))]
public sealed class NamedFilterCollection
{
    /// <summary>
    /// The schema version this library reads and writes. Reads reject any
    /// other version (fail-loud); see
    /// <see cref="NamedFilterCollectionJsonConverter"/>.
    /// </summary>
    public const int CurrentSchemaVersion = 1;

    /// <summary>
    /// The document's name rule — the single definition of "same name" for
    /// duplicate rejection, lookup, replace, removal, and the canonical sort
    /// (see the type remarks). Internal so the converter's wire-level
    /// duplicate check applies the same rule.
    /// </summary>
    internal static readonly StringComparer NameComparer = StringComparer.OrdinalIgnoreCase;

    private readonly ImmutableSortedDictionary<string, FilterConfig> _filters;

    /// <summary>The empty collection: no saved filters.</summary>
    public static NamedFilterCollection Empty { get; } =
        new(ImmutableSortedDictionary.Create<string, FilterConfig>(NameComparer));

    private NamedFilterCollection(ImmutableSortedDictionary<string, FilterConfig> filters)
    {
        _filters = filters;
        Names = filters.Keys.ToImmutableArray();
    }

    /// <summary>Number of saved filters in this collection.</summary>
    public int Count => _filters.Count;

    /// <summary>
    /// The saved-filter names in canonical (name-sorted) order — what a pick
    /// list binds. Display case is as typed at each name's latest
    /// <see cref="With"/>.
    /// </summary>
    public IReadOnlyList<string> Names { get; }

    /// <summary>
    /// Whether a saved filter with this name exists, per the name rule
    /// (case-insensitive) — e.g. a consumer's save-as overwrite check.
    /// </summary>
    /// <param name="name">The name to look up.</param>
    /// <exception cref="ArgumentNullException">
    /// Thrown when <paramref name="name"/> is <see langword="null"/>.
    /// </exception>
    public bool Contains(string name)
    {
        ArgumentNullException.ThrowIfNull(name);
        return _filters.ContainsKey(name);
    }

    /// <summary>
    /// Retrieves the named filter's configuration as a fresh snapshot — the
    /// caller's to bind and mutate freely; saving an edit back is an explicit
    /// <see cref="With"/>. Every call returns a new instance (see the
    /// snapshot contract in the type remarks). Lookup follows the name rule
    /// (case-insensitive).
    /// </summary>
    /// <param name="name">The name to look up.</param>
    /// <returns>A fresh copy of the saved configuration.</returns>
    /// <exception cref="ArgumentNullException">
    /// Thrown when <paramref name="name"/> is <see langword="null"/>.
    /// </exception>
    /// <exception cref="KeyNotFoundException">
    /// Thrown when no saved filter has this name.
    /// </exception>
    public FilterConfig GetConfig(string name)
    {
        ArgumentNullException.ThrowIfNull(name);
        if (!_filters.TryGetValue(name, out var stored))
            throw new KeyNotFoundException($"No saved filter named '{name}'.");
        return Snapshot(stored);
    }

    /// <summary>
    /// Non-throwing counterpart to <see cref="GetConfig"/>. Yields
    /// <see langword="null"/> on a miss — deliberately unlike
    /// <see cref="TryFromJson"/>'s always-usable out: a failed restore has a
    /// sensible fallback (the empty collection), but a lookup miss does not,
    /// and a default config here would silently filter nothing. The
    /// annotation makes ignoring the return value a compiler diagnostic
    /// rather than a runtime surprise.
    /// </summary>
    /// <param name="name">The name to look up.</param>
    /// <param name="config">
    /// On return, a fresh snapshot of the saved configuration, or
    /// <see langword="null"/> when no saved filter has this name.
    /// </param>
    /// <returns>
    /// <see langword="true"/> if a saved filter with this name exists.
    /// </returns>
    /// <exception cref="ArgumentNullException">
    /// Thrown when <paramref name="name"/> is <see langword="null"/>.
    /// </exception>
    public bool TryGetConfig(string name, [NotNullWhen(true)] out FilterConfig? config)
    {
        ArgumentNullException.ThrowIfNull(name);
        if (_filters.TryGetValue(name, out var stored))
        {
            config = Snapshot(stored);
            return true;
        }

        config = null;
        return false;
    }

    /// <summary>
    /// Returns a new collection with this name bound to a snapshot of
    /// <paramref name="config"/> — added, or replaced if the name already
    /// exists per the name rule. This is the save-as operation, including
    /// save-under-an-existing-name-overwrites; whether to confirm the
    /// overwrite is the consumer's UI concern. On replace, the new spelling
    /// wins along with the new config.
    /// </summary>
    /// <param name="name">
    /// The filter's name: non-blank, with no leading or trailing whitespace
    /// (validated, not trimmed — the caller normalizes user input).
    /// </param>
    /// <param name="config">
    /// The configuration to save. The document stores a snapshot of its
    /// current value, never this instance — see the type remarks.
    /// </param>
    /// <returns>The new collection; this instance is unchanged.</returns>
    /// <exception cref="ArgumentNullException">
    /// Thrown when <paramref name="name"/> or <paramref name="config"/> is
    /// <see langword="null"/>.
    /// </exception>
    /// <exception cref="ArgumentException">
    /// Thrown when <paramref name="name"/> is empty or whitespace-only, or
    /// carries leading or trailing whitespace.
    /// </exception>
    public NamedFilterCollection With(string name, FilterConfig config)
    {
        ValidateName(name);
        ArgumentNullException.ThrowIfNull(config);

        // Remove-then-add rather than SetItem: SetItem retains the existing
        // key's spelling on replace, and the contract is last-write-wins for
        // the display case too.
        return new(_filters.Remove(name).Add(name, Snapshot(config)));
    }

    /// <summary>
    /// Returns a collection without the named filter, per the name rule
    /// (case-insensitive). A missing name is a no-op returning this same
    /// instance — deletion is idempotent.
    /// </summary>
    /// <param name="name">The name to remove.</param>
    /// <returns>
    /// The new collection, or this instance when the name was not present.
    /// </returns>
    /// <exception cref="ArgumentNullException">
    /// Thrown when <paramref name="name"/> is <see langword="null"/>.
    /// </exception>
    public NamedFilterCollection Without(string name)
    {
        ArgumentNullException.ThrowIfNull(name);
        var updated = _filters.Remove(name);
        return ReferenceEquals(updated, _filters) ? this : new(updated);
    }

    /// <summary>
    /// The stored entries in canonical order, for the converter's write path.
    /// Values are the document's private snapshots — internal callers must
    /// neither leak nor mutate them.
    /// </summary>
    internal IEnumerable<KeyValuePair<string, FilterConfig>> CanonicalEntries => _filters;

    /// <summary>
    /// The boundary deep copy: the canonical serialize/deserialize
    /// round-trip, which is already the single, tested definition of a
    /// config's value — no separate <c>Clone</c> whose deep-copy semantics
    /// would need independent specification. Also normalizes: what is stored
    /// is byte-identical to what the wire will carry.
    /// </summary>
    private static FilterConfig Snapshot(FilterConfig config) =>
        FilterConfig.FromJson(config.ToJson());

    private static void ValidateName(string name)
    {
        ArgumentNullException.ThrowIfNull(name);
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Filter name must be non-blank.", nameof(name));
        if (name != name.Trim())
            throw new ArgumentException(
                $"Filter name must not carry leading or trailing whitespace (got '{name}').",
                nameof(name));
    }

    /// <summary>
    /// The single source of truth for how a
    /// <see cref="NamedFilterCollection"/> reaches a serializer: the
    /// source-generated metadata for this type, off
    /// <see cref="XgFilterJsonContext"/>
    /// (halheinrich/backgammon#129 leg 4). It replaces a bare
    /// <c>JsonSerializer.Serialize(this)</c> against the reflection-based
    /// default resolver, and carries no policy of its own — the bundled
    /// <see cref="NamedFilterCollectionJsonConverter"/> writes and reads the
    /// whole envelope by hand, and each entry's body is delegated to
    /// <see cref="FilterConfig.ToJson"/> / <see cref="FilterConfig.FromJson"/>,
    /// so nothing about this document is options-derived except the
    /// whitespace of the writer the serializer creates.
    /// </summary>
    private static JsonTypeInfo<NamedFilterCollection> CanonicalTypeInfo =>
        XgFilterJsonContext.Default.NamedFilterCollection;

    /// <summary>
    /// Serializes this collection to its canonical JSON representation — the
    /// inverse of <see cref="FromJson"/>. See
    /// <see cref="NamedFilterCollectionJsonConverter"/> for the pinned wire
    /// format.
    /// </summary>
    /// <returns>A JSON object string carrying every entry of this collection.</returns>
    public string ToJson() => JsonSerializer.Serialize(this, CanonicalTypeInfo);

    /// <summary>
    /// Deserializes a <see cref="NamedFilterCollection"/> from its canonical
    /// JSON representation — the inverse of <see cref="ToJson"/>. Fail-loud:
    /// see <see cref="NamedFilterCollectionJsonConverter"/> for everything a
    /// read rejects.
    /// </summary>
    /// <param name="json">A JSON object string, typically produced by <see cref="ToJson"/>.</param>
    /// <returns>The materialized collection.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="json"/> is null.</exception>
    /// <exception cref="ArgumentException">
    /// <paramref name="json"/> is the literal <c>null</c> token, which yields
    /// no collection.
    /// </exception>
    /// <exception cref="JsonException"><paramref name="json"/> is malformed or violates the wire contract.</exception>
    public static NamedFilterCollection FromJson(string json)
    {
        ArgumentNullException.ThrowIfNull(json);

        return JsonSerializer.Deserialize(json, CanonicalTypeInfo)
            ?? throw new ArgumentException(
                "JSON deserialized to a null collection; expected a NamedFilterCollection object.",
                nameof(json));
    }

    /// <summary>
    /// Non-throwing counterpart to <see cref="FromJson"/>. Absorbs the three
    /// ways a restore can fail — a null <paramref name="json"/> (e.g. a file
    /// that does not exist), the literal <c>null</c> token, or
    /// malformed/contract-violating JSON — and yields <see cref="Empty"/> in
    /// each case. This single-sources the "absent or corrupt input restores
    /// to the empty collection" policy so consumers need no knowledge of the
    /// JSON representation or its exception taxonomy. A consumer that wants
    /// to preserve a corrupt file for the user to repair (rather than
    /// overwrite it with the empty fallback) reacts to the
    /// <see langword="false"/> return.
    /// </summary>
    /// <param name="json">
    /// The candidate JSON, or null. Typically read straight from a
    /// persistence store whose contents the caller does not control.
    /// </param>
    /// <param name="collection">
    /// On return, always a usable collection: the restored instance on
    /// success, or <see cref="Empty"/> on failure.
    /// </param>
    /// <returns>
    /// <see langword="true"/> if <paramref name="json"/> was successfully
    /// deserialized; <see langword="false"/> otherwise, letting a caller
    /// react to a failed restore without catching exceptions.
    /// </returns>
    public static bool TryFromJson(string? json, out NamedFilterCollection collection)
    {
        if (json is not null)
        {
            try
            {
                if (JsonSerializer.Deserialize(json, CanonicalTypeInfo) is { } parsed)
                {
                    collection = parsed;
                    return true;
                }
            }
            catch (JsonException)
            {
                // Malformed or contract-violating JSON falls through to the
                // empty fallback below; any other (unexpected) exception is
                // intentionally left to propagate.
            }
        }

        collection = Empty;
        return false;
    }
}
