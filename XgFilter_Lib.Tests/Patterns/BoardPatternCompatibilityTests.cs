using BgDataTypes_Lib;
using XgFilter_Lib.Filtering;
using XgFilter_Lib.Patterns;
using XgFilter_Lib.Tests.Helpers;

namespace XgFilter_Lib.Tests.Patterns;

/// <summary>
/// Rule 7 of halheinrich/backgammon#268: the span grammar leaves every
/// existing pattern alone. A bracket list valid before the change, and not
/// refused by the new bar rule (rule 5), parses to an equal pattern, renders
/// the same text, evaluates the same, and rides the JSON wire as the same
/// bracket-list string.
///
/// <para>
/// The pre-change grammar is written out here as an independent oracle
/// (<see cref="LegacyIsValid"/>, <see cref="LegacyValue"/>) rather than read
/// back off the library, so a change to the library's single-location
/// validation or evaluation cannot move the oracle along with it.
/// </para>
/// </summary>
public class BoardPatternCompatibilityTests
{
    private static readonly string[] _heads =
        [.. Enumerable.Range(0, 26).Select(i => i.ToString(System.Globalization.CultureInfo.InvariantCulture)), "off", "opp-off"];

    private static readonly int?[] _bounds =
        [null, .. Enumerable.Range(-16, 33).Select(i => (int?)i)];

    /// <summary>Pre-change validity of a single token: each bound in the head's interval, min ≤ max.</summary>
    private static bool LegacyIsValid(string head, int? min, int? max)
    {
        var (lo, hi) = head switch
        {
            "off" => (0, 15),
            "opp-off" => (-15, 0),
            _ => (-15, 15),
        };
        bool Within(int? bound) => bound is not { } b || (lo <= b && b <= hi);
        return Within(min) && Within(max) && !(min > max);
    }

    /// <summary>Rule 5: the bars hold one side each.</summary>
    private static bool RefusedByBarRule(string head, int? min, int? max) =>
        (head == "0" && (min > 0 || max > 0)) || (head == "25" && (min < 0 || max < 0));

    /// <summary>Pre-change signed value at a head: the board entry, or a derived off count.</summary>
    private static int LegacyValue(string head, BoardPosition board)
    {
        var slots = Enumerable.Range(0, 26).Select(i => board[i]);
        return head switch
        {
            "off" => 15 - slots.Where(v => v > 0).Sum(),
            "opp-off" => -(15 + slots.Where(v => v < 0).Sum()),
            _ => board[int.Parse(head, System.Globalization.CultureInfo.InvariantCulture)],
        };
    }

    /// <summary>
    /// The standard start, a bar-heavy position, the empty board, and random
    /// positions — drawn at random and kept only when they form a position,
    /// since a pattern is only ever asked about one.
    /// </summary>
    private static IEnumerable<BoardPosition> SampleBoards()
    {
        yield return BoardPosition.Standard;
        yield return BoardBuilder.Build((25, 2), (6, 4), (0, -3), (19, -12));
        yield return BoardPosition.Empty;

        var rng = new Random(7);
        var counts = new int[26];
        for (int n = 0; n < 40;)
        {
            for (int i = 0; i < 26; i++)
                counts[i] = i switch
                {
                    0 => rng.Next(-2, 1),
                    25 => rng.Next(0, 3),
                    _ => rng.Next(-2, 3),
                };
            if (BoardPosition.TryCreate(counts, out var board))
            {
                n++;
                yield return board;
            }
        }
    }

    [Fact]
    public void EverySingleToken_KeepsItsVerdict_TextAndMeaning_ExceptTheBarRule()
    {
        var boards = SampleBoards().ToArray();
        int kept = 0, refusedByBarRule = 0;

        foreach (var head in _heads)
            foreach (var min in _bounds)
                foreach (var max in _bounds)
                {
                    var text = $"[{head},{min},{max}]";
                    bool legacyValid = LegacyIsValid(head, min, max);
                    bool barRule = RefusedByBarRule(head, min, max);

                    bool parsed = BoardPattern.TryParse(text, out var pattern);

                    if (!legacyValid || barRule)
                    {
                        parsed.Should().BeFalse("'{0}' must be refused", text);
                        if (legacyValid) refusedByBarRule++;
                        continue;
                    }

                    parsed.Should().BeTrue("'{0}' was valid before the change and rule 5 does not touch it", text);
                    pattern!.ToBracketList().Should().Be(text);
                    pattern.Constraints.Should().ContainSingle()
                        .Which.Should().BeOfType<CheckerRange>();

                    foreach (var board in boards)
                    {
                        int value = LegacyValue(head, board);
                        bool legacyVerdict = (min ?? int.MinValue) <= value && value <= (max ?? int.MaxValue);
                        pattern.Matches(board).Should().Be(legacyVerdict, "'{0}' on board [{1}]", text, board);
                    }

                    kept++;
                }

        // Guard the sweep itself: it must have kept most tokens and refused
        // exactly the bar-rule ones (each bar keeps its own sign's half).
        kept.Should().BeGreaterThan(7000);
        refusedByBarRule.Should().BeGreaterThan(0);
    }

    /// <summary>Existing multi-token lists, the committed oracle templates among them.</summary>
    public static TheoryData<string> LegacyBracketLists() => new()
    {
        BoardPatternOracleTests.VsTwoPlusUpPattern,
        BoardPatternOracleTests.Holding1386Vs20Template,
        BoardPatternOracleTests.InnerBoard631Pattern,
        BoardPatternOracleTests.InnerBoard54321Pattern,
        "[0,,-1] [5,2,] [6,,0] [7,0,0] [13,,]",
        "[0,,-1] [6,2,] [off,1,15] [opp-off,-15,-2]",
        "[6,2,] [5,,-2] [12,,-2] [off,1,] [opp-off,,-2]",
        "[6,-2,3] [13,-15,15] [25,0,] [0,,0]",
    };

    [Theory]
    [MemberData(nameof(LegacyBracketLists))]
    public void LegacyList_RendersTheSameText(string text)
    {
        BoardPattern.Parse(text).ToBracketList().Should().Be(text);
    }

    [Theory]
    [MemberData(nameof(LegacyBracketLists))]
    public void LegacyList_RidesTheWireAsTheSameBracketListString(string text)
    {
        // The config stores the text a pattern renders to, so a legacy list
        // placed as its own bracket list rides the wire unchanged, loads
        // equal, and still parses to the pattern it always meant.
        var config = new FilterConfig { PositionPattern = BoardPattern.Parse(text).ToBracketList() };

        var json = config.ToJson();
        var restored = FilterConfig.FromJson(json);

        json.Should().Contain($"\"PositionPattern\":\"{text}\"");
        restored.Should().Be(config);
        restored.PositionPattern.Should().Be(text);
        restored.GetInvalidFields().Should().BeEmpty();
        BoardPattern.Parse(restored.PositionPattern!).ToBracketList().Should().Be(text);
    }

    [Fact]
    public void SpanPattern_RidesTheSameWire()
    {
        const string text = "[7-12,3,] [6,2,] [24-25,-2,-2]";
        var config = new FilterConfig { PositionPattern = BoardPattern.Parse(text).ToBracketList() };

        var json = config.ToJson();

        json.Should().Contain($"\"PositionPattern\":\"{text}\"");
        FilterConfig.FromJson(json).Should().Be(config);
    }
}
