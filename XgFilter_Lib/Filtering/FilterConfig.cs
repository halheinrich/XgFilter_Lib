using System.Text.Json;
using System.Text.Json.Serialization;
using XgFilter_Lib.Enums;

namespace XgFilter_Lib.Filtering;

/// <summary>
/// Serializable configuration for a <see cref="DecisionFilterSet"/>.
/// Wire-friendly (default-constructible, mutable, JSON-round-trippable
/// with <c>JsonStringEnumConverter</c>) so consumers can bind it to UI
/// state on one side and POST it across a process boundary to be
/// materialized on the other.
///
/// <para>
/// Empty-list semantics (matches what consumer-side glue used to do
/// before this type existed): an empty <see cref="Players"/>,
/// <see cref="MatchScores"/>, <see cref="PositionTypes"/>, or
/// <see cref="PlayTypes"/> means "no filter of this kind is active" —
/// not "reject everything." <see cref="Build"/> simply skips adding
/// the filter to the set in that case. <see cref="DecisionType"/>
/// defaults to <see cref="DecisionTypeOption.Both"/>, which is a
/// no-op in <see cref="DecisionTypeFilter"/>.
/// </para>
///
/// <para>
/// Range filters (<see cref="ErrorRangeFilter"/>,
/// <see cref="MoveNumberFilter"/>) are added if either bound is set;
/// both-null pairs are skipped.
/// </para>
/// </summary>
public sealed class FilterConfig
{
    /// <summary>Player names whose decisions should pass; empty = no player filter.</summary>
    public IList<string> Players { get; set; } = new List<string>();

    /// <summary>Which decision types to admit. Defaults to <see cref="DecisionTypeOption.Both"/>.</summary>
    public DecisionTypeOption DecisionType { get; set; } = DecisionTypeOption.Both;

    /// <summary>
    /// Match-score tokens to admit (e.g. <c>"3a5a"</c>, <c>"1a5aC"</c>,
    /// <c>"money"</c>). Empty = no score filter.
    /// </summary>
    public IList<string> MatchScores { get; set; } = new List<string>();

    /// <summary>Inclusive lower bound on filter-error; null = open lower bound.</summary>
    public double? ErrorMin { get; set; }

    /// <summary>Inclusive upper bound on filter-error; null = open upper bound.</summary>
    public double? ErrorMax { get; set; }

    /// <summary>Inclusive lower bound on move number; null = open lower bound.</summary>
    public int? MoveNumberMin { get; set; }

    /// <summary>Inclusive upper bound on move number; null = open upper bound.</summary>
    public int? MoveNumberMax { get; set; }

    /// <summary>Position types to admit (OR semantics). Empty = no position-type filter.</summary>
    public IList<PositionType> PositionTypes { get; set; } = new List<PositionType>();

    /// <summary>Play types to admit (OR semantics). Empty = no play-type filter.</summary>
    public IList<PlayType> PlayTypes { get; set; } = new List<PlayType>();

    /// <summary>
    /// Materializes this configuration as a <see cref="DecisionFilterSet"/>.
    /// Each filter is added only when its corresponding configuration is
    /// non-empty / non-default; see the type-level remarks for the
    /// empty-list semantics.
    /// </summary>
    /// <exception cref="ArgumentException">
    /// <see cref="MatchScores"/> contains a malformed token — see
    /// <see cref="MatchScoreFilter"/>.
    /// </exception>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <see cref="PositionTypes"/> or <see cref="PlayTypes"/> contains
    /// an undefined enum value.
    /// </exception>
    public DecisionFilterSet Build()
    {
        var set = new DecisionFilterSet();

        if (Players.Count > 0)
            set.Add(new PlayerFilter(Players));

        if (DecisionType != DecisionTypeOption.Both)
            set.Add(new DecisionTypeFilter(DecisionType));

        if (MatchScores.Count > 0)
            set.Add(new MatchScoreFilter(MatchScores));

        if (ErrorMin.HasValue || ErrorMax.HasValue)
            set.Add(new ErrorRangeFilter(ErrorMin, ErrorMax));

        if (MoveNumberMin.HasValue || MoveNumberMax.HasValue)
            set.Add(new MoveNumberFilter(MoveNumberMin, MoveNumberMax));

        if (PositionTypes.Count > 0)
            set.Add(new PositionTypeFilter(PositionTypes));

        if (PlayTypes.Count > 0)
            set.Add(new PlayTypeFilter(PlayTypes));

        return set;
    }

    // -----------------------------------------------------------------------
    //  Canonical JSON serialization
    // -----------------------------------------------------------------------

    /// <summary>
    /// The single source of truth for how a <see cref="FilterConfig"/> maps to
    /// and from JSON. Registers <see cref="JsonStringEnumConverter"/> so the
    /// enum-typed members (<see cref="DecisionType"/>, <see cref="PositionTypes"/>,
    /// <see cref="PlayTypes"/>) serialize as their declaration names rather than
    /// ordinals — none of those enum types carries a type-level
    /// <c>[JsonConverter]</c>, so without this they would round-trip as ints and
    /// silently rebind to the wrong member if the enum were ever reordered.
    /// Held as a cached, immutable instance: <see cref="JsonSerializerOptions"/>
    /// is expensive to build and thread-safe once first used.
    /// </summary>
    private static readonly JsonSerializerOptions CanonicalOptions = new()
    {
        Converters = { new JsonStringEnumConverter() },
    };

    /// <summary>
    /// Serializes this configuration to its canonical JSON representation —
    /// the inverse of <see cref="FromJson"/>. Enum values are written as their
    /// declaration names. This is the lib-owned wire format; consumers that
    /// persist or transmit a <see cref="FilterConfig"/> should round-trip
    /// through this pair rather than reaching for <see cref="JsonSerializer"/>
    /// directly, so the enum-as-string contract stays in one place.
    /// </summary>
    /// <returns>A JSON object string carrying every field of this instance.</returns>
    public string ToJson() => JsonSerializer.Serialize(this, CanonicalOptions);

    /// <summary>
    /// Deserializes a <see cref="FilterConfig"/> from its canonical JSON
    /// representation — the inverse of <see cref="ToJson"/>. Members absent
    /// from the JSON retain their type defaults (e.g. omitted lists materialize
    /// empty, omitted <see cref="DecisionType"/> stays
    /// <see cref="DecisionTypeOption.Both"/>), so a default-config blob and an
    /// empty object both round-trip to an equivalent instance.
    /// </summary>
    /// <param name="json">A JSON object string, typically produced by <see cref="ToJson"/>.</param>
    /// <returns>The materialized configuration.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="json"/> is null.</exception>
    /// <exception cref="ArgumentException">
    /// <paramref name="json"/> is the literal <c>null</c> token, which yields no
    /// configuration.
    /// </exception>
    /// <exception cref="JsonException"><paramref name="json"/> is malformed.</exception>
    public static FilterConfig FromJson(string json)
    {
        ArgumentNullException.ThrowIfNull(json);

        return JsonSerializer.Deserialize<FilterConfig>(json, CanonicalOptions)
            ?? throw new ArgumentException(
                "JSON deserialized to a null configuration; expected a FilterConfig object.",
                nameof(json));
    }

    /// <summary>
    /// Non-throwing counterpart to <see cref="FromJson"/>, following the
    /// <c>Parse</c>/<c>TryParse</c> convention. Absorbs the three ways a
    /// restore can fail — a null <paramref name="json"/> (e.g. a storage key
    /// that was never written), the literal <c>null</c> token, or malformed
    /// JSON — and yields a fresh default <see cref="FilterConfig"/> in each
    /// case. This single-sources the "absent or corrupt input restores to
    /// defaults" policy in the lib so consumers need no knowledge of the JSON
    /// representation or its exception taxonomy.
    /// </summary>
    /// <param name="json">
    /// The candidate JSON, or null. Typically read straight from a persistence
    /// store whose contents the caller does not control.
    /// </param>
    /// <param name="config">
    /// On return, always a usable configuration: the restored instance on
    /// success, or a fresh default on failure. A caller content with
    /// default-on-failure may ignore the return value and read
    /// <paramref name="config"/> directly.
    /// </param>
    /// <returns>
    /// <see langword="true"/> if <paramref name="json"/> was successfully
    /// deserialized; <see langword="false"/> if it was null, the <c>null</c>
    /// token, or malformed. A false result lets a caller react to a failed
    /// restore (e.g. clear the corrupt entry or record telemetry) without
    /// catching exceptions.
    /// </returns>
    public static bool TryFromJson(string? json, out FilterConfig config)
    {
        if (json is not null)
        {
            try
            {
                if (JsonSerializer.Deserialize<FilterConfig>(json, CanonicalOptions) is { } parsed)
                {
                    config = parsed;
                    return true;
                }
            }
            catch (JsonException)
            {
                // Malformed JSON falls through to the default below; any other
                // (unexpected) exception is intentionally left to propagate.
            }
        }

        config = new FilterConfig();
        return false;
    }
}
