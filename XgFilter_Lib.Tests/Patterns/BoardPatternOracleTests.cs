using XgFilter_Lib.Classification;
using XgFilter_Lib.Patterns;
using XgFilter_Lib.Tests.Helpers;

namespace XgFilter_Lib.Tests.Patterns;

/// <summary>
/// The reason the named <c>PositionType</c> classifiers stay in the codebase
/// through pattern-v1: they are the oracle. Each test pins a hand-written
/// <see cref="BoardPattern"/> against the classifier it is meant to reproduce
/// and asserts they agree across a broad board sample.
///
/// <para>
/// The guard-free classifiers (<c>VsTwoPlusUp</c>, <c>Holding1386Vs20</c>) are
/// pure structural predicates, so equivalence is asserted on <em>every</em>
/// sampled board. The guarded inner-board classifiers additionally require
/// <c>!race</c>; a structural pattern cannot express the race guard, so
/// equivalence is asserted only on contact boards — and a dedicated test shows
/// the two diverge on a race board, documenting that contact-vs-race is the
/// separate dimension a position pattern does not capture.
/// </para>
/// </summary>
public class BoardPatternOracleTests
{
    private static readonly RaceClassifier _race = new();

    // -----------------------------------------------------------------------
    //  Pattern strings — single source of truth, shared with the corpus oracle
    //  (BoardPatternCorpusOracleTests). Each is the BoardPattern claimed to
    //  reproduce the like-named classifier; the inner-board pair captures only
    //  the structural predicate and must be AND-composed with Contact to match
    //  the classifier's !race guard.
    // -----------------------------------------------------------------------

    internal const string VsTwoPlusUpPattern = "[0,,-2]";

    internal const string Holding1386Vs20Template =
        "[0,0,] [1,0,] [2,0,] [3,0,] [4,0,] [5,,-2] [6,2,] [7,0,0] [8,2,] " +
        "[9,0,0] [10,0,0] [11,0,0] [12,,-2] [13,2,] [14,,0] [15,,0] [16,,0] " +
        "[17,,0] [18,,0] [19,,0] [20,,0] [21,,0] [22,,0] [23,,0] [24,,0] [25,,0]";

    internal const string InnerBoard631Pattern = "[1,2,] [2,,1] [3,2,] [4,,1] [5,,1] [6,2,]";

    internal const string InnerBoard54321Pattern = "[1,2,] [2,2,] [3,2,] [4,2,] [5,2,] [6,,1]";

    // -----------------------------------------------------------------------
    //  VsTwoPlusUp ≡ [0,,-2] — guard-free, equivalence on every board
    // -----------------------------------------------------------------------

    [Fact]
    public void VsTwoPlusUp_Pattern_ReproducesClassifierExactly()
    {
        var pattern = BoardPattern.Parse(VsTwoPlusUpPattern);
        var classifier = new VsTwoPlusUpClassifier();

        AssertAgreesOnAll(pattern, classifier.Matches, SampleBoards());
    }

    // -----------------------------------------------------------------------
    //  Holding1386Vs20 ≡ its full template — guard-free, equivalence on all
    // -----------------------------------------------------------------------

    [Fact]
    public void Holding1386Vs20_Pattern_ReproducesClassifierExactly()
    {
        var pattern = BoardPattern.Parse(Holding1386Vs20Template);
        var classifier = new Holding1386Vs20Classifier();

        AssertAgreesOnAll(pattern, classifier.Matches, SampleBoards());
    }

    // -----------------------------------------------------------------------
    //  Inner-board classifiers — structural part only; race is the separate
    //  dimension, so equivalence holds on contact boards but not race boards.
    // -----------------------------------------------------------------------

    [Fact]
    public void InnerBoard631_StructuralPattern_ReproducesClassifierOnContactBoards()
    {
        var pattern = BoardPattern.Parse(InnerBoard631Pattern);
        var classifier = new InnerBoard631Classifier();

        AssertAgreesOnAll(pattern, classifier.Matches, ContactBoards());
    }

    [Fact]
    public void InnerBoard54321_StructuralPattern_ReproducesClassifierOnContactBoards()
    {
        var pattern = BoardPattern.Parse(InnerBoard54321Pattern);
        var classifier = new InnerBoard54321Classifier();

        AssertAgreesOnAll(pattern, classifier.Matches, ContactBoards());
    }

    [Fact]
    public void InnerBoard631_StructuralPattern_DivergesFromClassifierOnRaceBoard()
    {
        // The guard is the point of this divergence: a fully-raced board that
        // still carries the 6-3-1 inner structure satisfies the pattern but is
        // rejected by the classifier's !race guard. The pattern captures the
        // structural dimension only; contact-vs-race is orthogonal.
        var pattern = BoardPattern.Parse(InnerBoard631Pattern);
        var classifier = new InnerBoard631Classifier();

        // 6-3-1 made; opponent entirely ahead of the player's last checker → race.
        var racedInnerBoard = BoardBuilder.Build(
            (6, 2), (3, 2), (1, 4),            // player inner structure, all home
            (24, -8), (23, -7));               // opponent far ahead → playerLast < opponentFirst

        _race.Matches(racedInnerBoard).Should().BeTrue("the fixture must be a race for this test to mean anything");
        pattern.Matches(racedInnerBoard).Should().BeTrue();
        classifier.Matches(racedInnerBoard).Should().BeFalse();
    }

    // -----------------------------------------------------------------------
    //  Sampling — boundary perturbations of structural fixtures plus a
    //  deterministic random spread, so equivalence is tested right where the
    //  predicates flip, not only on far-from-boundary boards.
    // -----------------------------------------------------------------------

    private static readonly int[][] _fixtures =
    [
        // Starting position
        BoardBuilder.Build((24, 2), (13, 5), (8, 3), (6, 5), (1, -2), (12, -5), (17, -3), (19, -5)),
        // Vs 2+ up
        BoardBuilder.Build((0, -2), (24, 2), (13, 5), (8, 3), (6, 5), (12, -5), (17, -3), (19, -3)),
        // Holding 13-8-6 vs 20
        BoardBuilder.Build((13, 5), (8, 3), (6, 4), (4, 2), (1, 1), (5, -2), (12, -3), (19, -4), (21, -4), (23, -2)),
        // Inner-board 6-3-1, contact (opponent's first checker behind player's last)
        BoardBuilder.Build((6, 2), (3, 2), (1, 2), (13, 5), (8, 4), (12, -3), (20, -5), (22, -4), (24, -3)),
        // Inner-board 5-4-3-2-1, contact
        BoardBuilder.Build((5, 2), (4, 2), (3, 2), (2, 2), (1, 2), (13, 5), (12, -3), (20, -5), (22, -4), (24, -3)),
    ];

    /// <summary>
    /// Every sampled board: the fixtures themselves, single-index perturbations
    /// of each fixture across ±2 (clamped to the legal ±15), and a seeded
    /// random spread over a small signed range.
    /// </summary>
    private static IEnumerable<int[]> SampleBoards()
    {
        foreach (var fixture in _fixtures)
        {
            yield return fixture;
            for (int index = 0; index < 26; index++)
                foreach (int delta in new[] { -2, -1, 1, 2 })
                {
                    var board = (int[])fixture.Clone();
                    board[index] = Math.Clamp(board[index] + delta, -15, 15);
                    yield return board;
                }
        }

        var rng = new Random(20260621);
        for (int i = 0; i < 2000; i++)
        {
            var board = new int[26];
            for (int index = 0; index < 26; index++)
                board[index] = rng.Next(-4, 5);
            yield return board;
        }
    }

    private static IEnumerable<int[]> ContactBoards() =>
        SampleBoards().Where(b => !_race.Matches(b));

    /// <summary>
    /// Asserts the pattern and the classifier agree on every sampled board,
    /// reporting the first disagreement with the offending board.
    /// </summary>
    private static void AssertAgreesOnAll(
        BoardPattern pattern, Func<IReadOnlyList<int>, bool> classifier, IEnumerable<int[]> boards)
    {
        foreach (var board in boards)
        {
            pattern.Matches(board).Should().Be(
                classifier(board),
                "pattern and classifier must agree on board [{0}]", string.Join(",", board));
        }
    }
}
