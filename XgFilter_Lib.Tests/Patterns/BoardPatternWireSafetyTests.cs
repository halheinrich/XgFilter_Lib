using System.Text.Json;
using XgFilter_Lib.Filtering;
using XgFilter_Lib.Patterns;

namespace XgFilter_Lib.Tests.Patterns;

/// <summary>
/// Wire-safety regression guard for <see cref="BoardPattern"/> serialization.
///
/// <para>
/// These tests deliberately serialize through a <em>plain</em>
/// <see cref="JsonSerializerOptions"/> that does <strong>not</strong> register
/// <see cref="BoardPatternJsonConverter"/> — mirroring the BgDataTypes_Lib
/// convention for <c>CubeAction</c>/<c>BgDecisionData</c>, which "rely on the
/// attribute alone so that removing it would fail." The only thing carrying the
/// bracket-list wire form here is the type-level
/// <c>[JsonConverter(typeof(BoardPatternJsonConverter))]</c> on
/// <see cref="BoardPattern"/>. Remove that attribute (or let any wire path lose
/// the converter) and these tests go red — reproducing the
/// <c>ExtractFromXgToCsv</c> 500, where the default reflection serializer cannot
/// reconstruct the immutable <see cref="BoardPattern"/> (its constructor
/// parameter has no matching settable property).
/// </para>
/// </summary>
public class BoardPatternWireSafetyTests
{
    // A non-trivial pattern: mixed bounds, both signs, and both named
    // borne-off tokens, so a degenerate serializer that dropped fields — or a
    // wire path that lost the named-location vocabulary — couldn't accidentally
    // round-trip it.
    private const string Bracket = "[6,2,] [5,,-2] [12,,-2] [off,1,] [opp-off,,-2]";

    // No converter registered — the attribute on BoardPattern is the only thing
    // that can carry the bracket-list form across this wire.
    private static readonly JsonSerializerOptions PlainOptions = new();

    // Mirrors the exact options the app's wire path used when it 500'd.
    private static readonly JsonSerializerOptions WireOptions = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    // -----------------------------------------------------------------------
    //  FilterConfig carrying a PositionPattern — the field that 500'd
    // -----------------------------------------------------------------------

    [Fact]
    public void FilterConfig_PlainOptions_DoesNotThrowAndPatternRoundTrips()
    {
        var original = new FilterConfig { PositionPattern = BoardPattern.Parse(Bracket) };

        // (a) does not throw: serializing the immutable BoardPattern under
        // options with no registered converter only works via the attribute.
        var json = JsonSerializer.Serialize(original, PlainOptions);
        var restored = JsonSerializer.Deserialize<FilterConfig>(json, PlainOptions)!;

        // (b) the pattern round-trips equal. BoardPattern has no value-equality,
        // so compare via its bracket-list rendering.
        restored.PositionPattern.Should().NotBeNull();
        restored.PositionPattern!.ToBracketList().Should().Be(Bracket);
    }

    [Fact]
    public void FilterConfig_WireOptions_DoesNotThrowAndPatternRoundTrips()
    {
        // Pin the precise 500 scenario: the app deserialized with
        // PropertyNameCaseInsensitive = true and no BoardPatternJsonConverter.
        var original = new FilterConfig { PositionPattern = BoardPattern.Parse(Bracket) };

        var json = JsonSerializer.Serialize(original, WireOptions);
        var restored = JsonSerializer.Deserialize<FilterConfig>(json, WireOptions)!;

        restored.PositionPattern.Should().NotBeNull();
        restored.PositionPattern!.ToBracketList().Should().Be(Bracket);
    }

    // -----------------------------------------------------------------------
    //  Bare BoardPattern — the wire contract at the type, not just nested
    // -----------------------------------------------------------------------

    [Fact]
    public void BoardPattern_PlainOptions_DoesNotThrowAndRoundTrips()
    {
        var original = BoardPattern.Parse(Bracket);

        var json = JsonSerializer.Serialize(original, PlainOptions);
        var restored = JsonSerializer.Deserialize<BoardPattern>(json, PlainOptions)!;

        // Serializes as the bracket-list string itself, not a nested object.
        json.Should().Be($"\"{Bracket}\"");
        restored.Should().NotBeNull();
        restored.ToBracketList().Should().Be(Bracket);
    }
}
