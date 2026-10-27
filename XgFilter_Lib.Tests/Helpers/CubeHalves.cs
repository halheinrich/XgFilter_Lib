using BgDataTypes_Lib;
using BgDataTypes_Lib.TestSupport;

namespace XgFilter_Lib.Tests.Helpers;

/// <summary>
/// Cube records shaped by their two halves — the doubler's decision and,
/// where a double was offered, the taker's (SPEC-filtering §8) — for the
/// tests of the filter for the name and error facets and of what a filter set
/// reads. Alice is on roll and Bob the taker unless a builder says otherwise;
/// each is built through <see cref="TestRecords"/>.
/// </summary>
internal static class CubeHalves
{
    /// <summary>
    /// A cube decision <paramref name="doubler"/>, on roll, doubled and
    /// <paramref name="taker"/> passed: the doubler's error is
    /// <paramref name="doublerError"/> and the taker's
    /// <paramref name="takerError"/>, to rounding. Taking is right — the
    /// double/take equity, <c>1 − takerError</c>, is below the cash — so the
    /// pass costs <paramref name="takerError"/>; not doubling is worth
    /// <paramref name="doublerError"/> more than doubling against the take, so
    /// the double costs that (a tie favours not doubling, so a zero error is
    /// exact).
    /// </summary>
    public static CubeDecision DoubledAndPassed(
        double doublerError, double takerError, string? doubler = "Alice", string? taker = "Bob",
        PositionData? position = null)
    {
        double doubleTake = 1.0 - takerError;
        return TestRecords.Cube(
            position: position,
            decision: TestRecords.CubeData(
                noDoubleEquity: doubleTake + doublerError,
                doubleTakeEquity: doubleTake,
                userDoublerAction: CubeAction.Double,
                userTakerAction: CubeAction.Pass),
            descriptive: TestRecords.Descriptive(onRollName: doubler, opponentName: taker));
    }

    /// <summary>
    /// Alice doubled correctly (no double +0.512, double/take +0.634) and
    /// Bob's response is not stated; the analyser states his take error,
    /// <paramref name="takeError"/>.
    /// </summary>
    public static CubeDecision DoubledWithTheAnalysersTakeError(double takeError) =>
        TestRecords.Cube(decision: TestRecords.CubeData(
            userDoublerAction: CubeAction.Double, userTakerAction: null,
            unstatedTakerActionError: takeError));

    /// <summary>
    /// Alice doubled correctly, an error of 0, and Bob answered by resigning:
    /// a double offered, with neither a recorded response nor an analyser's
    /// take error (SPEC-filtering §8).
    /// </summary>
    public static CubeDecision DoubledAndUnanswered() =>
        TestRecords.Cube(decision: TestRecords.CubeData(
            userDoublerAction: CubeAction.Double, userTakerAction: null));

    /// <summary>
    /// Alice did not double on a double/take (no double +0.512, double/take
    /// +0.634): her error is 0.122, and no double was offered, so Bob made no
    /// decision.
    /// </summary>
    public static CubeDecision NotDoubled() =>
        TestRecords.Cube(decision: TestRecords.CubeData(
            userDoublerAction: CubeAction.NoDouble, userTakerAction: null));
}
