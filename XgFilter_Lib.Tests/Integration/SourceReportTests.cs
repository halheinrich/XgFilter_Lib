using BgDataTypes_Lib;
using BgDataTypes_Lib.TestSupport;
using ConvertXgToJson_Lib.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using XgFilter_Lib.Filtering;
using XgFilter_Lib.Tests.Helpers;

namespace XgFilter_Lib.Tests.Integration;

/// <summary>
/// The structured rejected-source result (halheinrich/backgammon#368): a
/// <see cref="SourceReport"/> the caller hands to any entry point records
/// which sources the walk attempted and rejected, and whether it finished.
/// Every walk here runs over sources synthesized through XG's writer
/// (<see cref="SyntheticSources"/>) plus malformed ones, so the report is
/// read off the real read path, never a stub; these tests read nothing from
/// <c>TestData</c> and gate.
/// </summary>
public class SourceReportTests
{
    private static readonly ILogger<FilteredDecisionIterator> NullLogger =
        NullLogger<FilteredDecisionIterator>.Instance;

    private static FilteredDecisionIterator NewIterator(DecisionFilterSet? filters = null) =>
        new(filters ?? new DecisionFilterSet(), PlayRanking.Equity, NullLogger);

    /// <summary>Two matches and a position: more than one source, both XG formats.</summary>
    private static readonly (string Name, XgFile File)[] ReadableFiles =
    [
        ("first.xg", SyntheticSources.TwoGameMatch()),
        ("second.xg", SyntheticSources.TwoGameMatch("P3", "P4")),
        ("position.xgp", SyntheticSources.Position()),
    ];

    /// <summary>
    /// <see cref="ReadableFiles"/> as fresh buffered streams, so the streams
    /// outlive the lazy enumeration and each walk reads its own.
    /// </summary>
    private static List<XgFileStream> Readable() =>
        [.. ReadableFiles.Select(s => SyntheticSources.XgEntry(s.Name, s.File))];

    /// <summary>A named stream the producer refuses: zero bytes, no header.</summary>
    private static XgFileStream Malformed(string name) => SyntheticSources.MalformedEntry(name);

    // -----------------------------------------------------------------------
    //  The four verdicts a consumer must be able to tell apart
    // -----------------------------------------------------------------------

    [Fact]
    public void OneMalformedAmongReadable_YieldsTheReadable_AndNamesTheRejected()
    {
        var streams = Readable();
        streams.Add(Malformed("malformed.xg"));
        var report = new SourceReport();

        var rows = NewIterator().IterateXgStreams(streams, report).ToList();

        rows.Should().NotBeEmpty("the readable sources still yield their decisions");
        rows.Should().OnlyContain(r => r.SourceFile != "malformed.xg");

        var rejection = report.Rejected.Should().ContainSingle().Which;
        rejection.SourceName.Should().Be("malformed.xg", "the name is the caller's, extension and all");
        rejection.Reason.Should().NotBeNull("the reason is the read's own exception, as data");

        report.AttemptedCount.Should().Be(streams.Count);
        report.ReadableCount.Should().Be(streams.Count - 1);
        report.IsComplete.Should().BeTrue();
        report.AllRejected.Should().BeFalse("one rejected file among readable ones is an incomplete selection, not an unreadable one");
    }

    [Fact]
    public void ReadableSourcesWithNoMatchingDecision_AreReadable_NotRejected()
    {
        // A successfully read source counts as readable however the filters
        // judge its decisions: zero matches is the filter's verdict, not the
        // file's.
        var streams = Readable();
        var report = new SourceReport();

        var rows = NewIterator(new DecisionFilterSet().Add(new MatchNothing()))
            .IterateXgStreams(streams, report).ToList();

        rows.Should().BeEmpty();
        report.AttemptedCount.Should().Be(streams.Count);
        report.ReadableCount.Should().Be(streams.Count);
        report.Rejected.Should().BeEmpty();
        report.IsComplete.Should().BeTrue();
        report.AllRejected.Should().BeFalse();
    }

    [Fact]
    public void EmptyInput_IsACompletedWalkOverNothing()
    {
        var report = new SourceReport();

        NewIterator().IterateXgStreams([], report).Should().BeEmpty();

        report.AttemptedCount.Should().Be(0);
        report.ReadableCount.Should().Be(0);
        report.Rejected.Should().BeEmpty();
        report.IsComplete.Should().BeTrue();
        report.AllRejected.Should().BeFalse("nothing was attempted, so nothing was rejected");
    }

    [Fact]
    public void EverySourceMalformed_IsAllRejected()
    {
        var streams = new[] { Malformed("one.xg"), Malformed("two.xgp") };
        var report = new SourceReport();

        NewIterator().IterateXgStreams(streams, report).Should().BeEmpty();

        report.AttemptedCount.Should().Be(2);
        report.ReadableCount.Should().Be(0);
        report.Rejected.Select(r => r.SourceName).Should().Equal("one.xg", "two.xgp");
        report.IsComplete.Should().BeTrue();
        report.AllRejected.Should().BeTrue();
    }

    // -----------------------------------------------------------------------
    //  One report, one walk
    // -----------------------------------------------------------------------

    [Fact]
    public void EarlyTermination_LeavesTheReportIncomplete_AndDrawsNoConclusion()
    {
        // The malformed stream comes first, so by the time the first decision
        // is yielded it has been rejected; the consumer then stops. What was
        // reached is reported; nothing is concluded about the rest.
        var streams = new List<XgFileStream> { Malformed("malformed.xg") };
        streams.AddRange(Readable());
        var report = new SourceReport();

        _ = NewIterator().IterateXgStreams(streams, report).First();

        report.Rejected.Select(r => r.SourceName).Should().Equal("malformed.xg");
        report.AttemptedCount.Should().Be(2, "the rejected stream and the one that yielded");
        report.IsComplete.Should().BeFalse();
        report.AllRejected.Should().BeFalse();
    }

    [Fact]
    public void AReportPassedToASecondEnumeration_IsRefused_AndKeepsItsRecord()
    {
        var streams = Readable();
        streams.Add(Malformed("malformed.xg"));
        var report = new SourceReport();
        var iterator = NewIterator();

        _ = iterator.IterateXgStreams(streams, report).ToList();
        var attempted = report.AttemptedCount;

        var second = () => iterator.IterateXgStreams(Readable(), report).ToList();
        second.Should().Throw<InvalidOperationException>();

        report.AttemptedCount.Should().Be(attempted, "the completed report is a record of its walk");
        report.Rejected.Should().ContainSingle();
        report.IsComplete.Should().BeTrue();
    }

    [Fact]
    public void ReEnumeratingTheSameEnumerable_IsRefusedToo()
    {
        var report = new SourceReport();
        var enumerable = NewIterator().IterateXgStreams(Readable(), report);

        _ = enumerable.ToList();

        var again = () => enumerable.ToList();
        again.Should().Throw<InvalidOperationException>(
            "a second walk would append to a report that belongs to the first");
    }

    [Fact]
    public void AReportIsClaimedOnTheFirstStep_NotAtTheCall()
    {
        // Lazy like the walk itself: obtaining two enumerables over one
        // report is fine until one of them moves; a report never claimed
        // reads as nothing attempted and not complete.
        var report = new SourceReport();
        var iterator = NewIterator();

        var first = iterator.IterateXgStreams(Readable(), report);
        var second = iterator.IterateXgStreams(Readable(), report);

        report.AttemptedCount.Should().Be(0);
        report.IsComplete.Should().BeFalse();

        _ = first.ToList();
        var walkSecond = () => second.ToList();
        walkSecond.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void OneIteratorAcrossEnumerations_EachReportStandsAlone()
    {
        // BgQuiz's WasmUploadedProblemSetSource keeps one iterator and
        // re-enumerates it per Start; each walk gets its own report, and an
        // earlier one is untouched by a later walk.
        var iterator = NewIterator();
        var first = new SourceReport();
        var second = new SourceReport();

        var withBad = Readable();
        withBad.Add(Malformed("malformed.xg"));
        _ = iterator.IterateXgStreamDiagrams(withBad, first).ToList();
        _ = iterator.IterateXgStreamDiagrams(Readable(), second).ToList();

        first.Rejected.Should().ContainSingle();
        first.AttemptedCount.Should().Be(withBad.Count);
        first.IsComplete.Should().BeTrue();

        second.Rejected.Should().BeEmpty();
        second.AttemptedCount.Should().Be(withBad.Count - 1);
        second.IsComplete.Should().BeTrue();
    }

    // -----------------------------------------------------------------------
    //  One shape for every entry point
    // -----------------------------------------------------------------------

    [Fact]
    public void DirectoryOverloads_ReportLikeTheStreamOverloads()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(tempDir);

        try
        {
            foreach (var (name, file) in ReadableFiles)
                File.WriteAllBytes(Path.Combine(tempDir, name), SyntheticSources.XgBytes(file));
            File.WriteAllBytes(Path.Combine(tempDir, "malformed.xg"), []);
            int fileCount = Directory.GetFiles(tempDir).Length;

            var iterator = NewIterator();
            var rows = new SourceReport();
            var diagrams = new SourceReport();
            var streams = new SourceReport();

            _ = iterator.IterateXgDirectory(tempDir, rows).ToList();
            _ = iterator.IterateXgDirectoryDiagrams(tempDir, diagrams).ToList();
            _ = iterator.IterateXgStreams(
                Directory.GetFiles(tempDir).Select(p => new XgFileStream(
                    Path.GetFileName(p), new MemoryStream(File.ReadAllBytes(p)))),
                streams).ToList();

            foreach (var report in new[] { rows, diagrams, streams })
            {
                report.AttemptedCount.Should().Be(fileCount);
                report.Rejected.Select(r => r.SourceName).Should().Equal("malformed.xg");
                report.Rejected[0].Reason.Should().NotBeNull();
                report.IsComplete.Should().BeTrue();
                report.AllRejected.Should().BeFalse();
            }
        }
        finally
        {
            Directory.Delete(tempDir, recursive: true);
        }
    }

    [Fact]
    public void JsonDirectory_ReportsAFileThatDoesNotParse()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(tempDir);

        try
        {
            File.WriteAllText(Path.Combine(tempDir, "bad.json"), "{");
            var report = new SourceReport();

            NewIterator().IterateJsonDirectory(tempDir, report).Should().BeEmpty();

            report.Rejected.Select(r => r.SourceName).Should().Equal("bad.json");
            report.AllRejected.Should().BeTrue();
        }
        finally
        {
            Directory.Delete(tempDir, recursive: true);
        }
    }

    [Fact]
    public void IgnoringTheReport_YieldsExactlyWhatPassingOneDoes()
    {
        var iterator = NewIterator();
        var withBad = Readable();
        withBad.Add(Malformed("malformed.xg"));
        var withBadAgain = Readable();
        withBadAgain.Add(Malformed("malformed.xg"));

        var ignoring = iterator.IterateXgStreamDiagrams(withBad).Select(d => d.Id).ToList();
        var reporting = iterator.IterateXgStreamDiagrams(withBadAgain, new SourceReport()).Select(d => d.Id).ToList();

        reporting.Should().Equal(ignoring);
    }

    [Fact]
    public void TheSkipWarning_StaysAsATraceOfTheSameEvent()
    {
        // The report is the source of the fact; the log line is a trace of
        // it, carrying the very exception the report records.
        var spy = new RecordingLogger<FilteredDecisionIterator>();
        var iterator = new FilteredDecisionIterator(new DecisionFilterSet(), PlayRanking.Equity, spy);
        var report = new SourceReport();

        _ = iterator.IterateXgStreams([Malformed("malformed.xg")], report).ToList();

        var skip = spy.Entries.Should().ContainSingle(e => e.Message.Contains("malformed.xg")).Which;
        skip.Level.Should().Be(LogLevel.Warning);
        skip.Exception.Should().BeSameAs(report.Rejected[0].Reason);
    }

    [Fact]
    public void AThrowingLogger_PropagatesAfterTheRejectionIsRecorded()
    {
        // The record is written before the trace, so a logger that throws
        // cannot leave a source that failed to read counted as readable: the
        // report's partial counts stay true of the sources reached.
        var iterator = new FilteredDecisionIterator(
            new DecisionFilterSet(), PlayRanking.Equity, new ThrowingLogger<FilteredDecisionIterator>());
        var report = new SourceReport();

        var act = () => iterator.IterateXgStreams([Malformed("malformed.xg")], report).ToList();
        act.Should().Throw<InvalidOperationException>().WithMessage($"*{ThrowingLogger<FilteredDecisionIterator>.Failure}*");

        var rejection = report.Rejected.Should().ContainSingle().Which;
        rejection.SourceName.Should().Be("malformed.xg");
        rejection.Reason.Should().NotBeOfType<InvalidOperationException>(
            "the rejection carries the read's own exception, never the logger's");
        report.AttemptedCount.Should().Be(1);
        report.ReadableCount.Should().Be(0);
        report.IsComplete.Should().BeFalse();
        report.AllRejected.Should().BeFalse("nothing is concluded from a walk that did not finish");
    }

    // -----------------------------------------------------------------------
    //  The boundaries that are not rejections
    // -----------------------------------------------------------------------

    [Fact]
    public void AnInvalidSourceName_IsAUsageError_NotARejection()
    {
        var streams = Readable().Take(1).ToList();
        streams.Add(new XgFileStream("noextension", new MemoryStream([1, 2, 3])));
        var report = new SourceReport();

        var act = () => NewIterator().IterateXgStreams(streams, report).ToList();
        act.Should().Throw<ArgumentException>();

        report.Rejected.Should().BeEmpty();
        report.AttemptedCount.Should().Be(1, "the valid stream before it was attempted; the bad name never was");
        report.IsComplete.Should().BeFalse();
    }

    [Fact]
    public void ANullStream_IsAUsageError_NotARejection()
    {
        var report = new SourceReport();

        var act = () => NewIterator().IterateXgStreams([new XgFileStream("x.xg", null!)], report).ToList();
        act.Should().Throw<ArgumentException>();

        report.Rejected.Should().BeEmpty();
        report.AttemptedCount.Should().Be(0);
        report.IsComplete.Should().BeFalse();
    }

    [Fact]
    public void AFailureEnumeratingTheSources_IsNotARejection()
    {
        var report = new SourceReport();

        var act = () => NewIterator().IterateXgStreams(OneReadableThenThrow(), report).ToList();
        act.Should().Throw<IOException>().WithMessage("*source enumeration*");

        report.AttemptedCount.Should().Be(1);
        report.Rejected.Should().BeEmpty();
        report.IsComplete.Should().BeFalse();
    }

    [Fact]
    public void AFilterFailure_IsNotARejection()
    {
        var report = new SourceReport();
        var iterator = NewIterator(new DecisionFilterSet().Add(new ThrowingFilter()));

        var act = () => iterator.IterateXgStreams(Readable(), report).ToList();
        act.Should().Throw<InvalidOperationException>().WithMessage("*filter failure*");

        report.Rejected.Should().BeEmpty();
        report.IsComplete.Should().BeFalse();
    }

    [Fact]
    public void AConsumerFailure_IsNotARejection()
    {
        var report = new SourceReport();

        var act = () =>
        {
            foreach (var _ in NewIterator().IterateXgStreams(Readable(), report))
                throw new InvalidOperationException("consumer failure");
        };
        act.Should().Throw<InvalidOperationException>().WithMessage("*consumer failure*");

        report.Rejected.Should().BeEmpty();
        report.IsComplete.Should().BeFalse();
    }

    // -----------------------------------------------------------------------
    //  The record itself
    // -----------------------------------------------------------------------

    [Fact]
    public void SourceRejection_RefusesABlankNameOrANullReason()
    {
        var blank = () => new SourceRejection(" ", new IOException());
        var nullReason = () => new SourceRejection("x.xg", null!);

        blank.Should().Throw<ArgumentException>();
        nullReason.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void Rejected_HandsOutAnImmutableList_ThatLaterRejectionsDoNotChange()
    {
        // Read mid-walk, after the first rejection and before the second: the
        // list a consumer holds is a snapshot, and the report's own grows.
        var report = new SourceReport();
        var streams = new List<XgFileStream> { Malformed("one.xg") };
        streams.AddRange(Readable());
        streams.Add(Malformed("two.xg"));
        IReadOnlyList<SourceRejection>? midWalk = null;

        foreach (var _ in NewIterator().IterateXgStreams(streams, report))
            midWalk ??= report.Rejected;

        midWalk.Should().NotBeNull("the readable sources yield at least one decision");
        midWalk!.Select(r => r.SourceName).Should().Equal("one.xg");
        report.Rejected.Select(r => r.SourceName).Should().Equal("one.xg", "two.xg");
        midWalk.Should().NotBeAssignableTo<List<SourceRejection>>();
        midWalk.Should().NotBeAssignableTo<SourceRejection[]>();
    }

    // -----------------------------------------------------------------------
    //  Doubles
    // -----------------------------------------------------------------------

    private static IEnumerable<XgFileStream> OneReadableThenThrow()
    {
        yield return Readable()[0];
        throw new IOException("source enumeration failed");
    }

    private sealed class MatchNothing : IDecisionFilter
    {
        public bool Matches(IDecisionFilterData data) => false;
    }

    private sealed class ThrowingFilter : IDecisionFilter
    {
        public bool Matches(IDecisionFilterData data) =>
            throw new InvalidOperationException("filter failure");
    }
}
