using BgDataTypes_Lib;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using XgFilter_Lib.Filtering;

namespace XgFilter_Lib.Tests.Integration;

/// <summary>
/// The structured rejected-source result (halheinrich/backgammon#368): a
/// <see cref="SourceReport"/> the caller hands to any entry point records
/// which sources the walk attempted and rejected, and whether it finished.
/// Every walk here runs over the fixture corpus plus synthesized bad sources,
/// so the report is read off the real read path, never a stub.
/// </summary>
public class SourceReportTests
{
    private static readonly string FixtureDir = Path.Combine(
        AppContext.BaseDirectory, "TestData", "FixtureFiles");

    private static readonly ILogger<FilteredDecisionIterator> NullLogger =
        NullLogger<FilteredDecisionIterator>.Instance;

    private static FilteredDecisionIterator NewIterator(DecisionFilterSet? filters = null) =>
        new(filters ?? new DecisionFilterSet(), PlayRanking.Equity, NullLogger);

    /// <summary>
    /// Every XG-format fixture as a buffered stream, so the streams outlive
    /// the lazy enumeration. Fixture-agnostic.
    /// </summary>
    private static List<XgFileStream> Fixtures() =>
        Directory.GetFiles(FixtureDir, "*.xg")
            .Concat(Directory.GetFiles(FixtureDir, "*.xgp"))
            .Select(p => new XgFileStream(
                Path.GetFileName(p), new MemoryStream(File.ReadAllBytes(p))))
            .ToList();

    /// <summary>A named stream the producer refuses: zero bytes, no header.</summary>
    private static XgFileStream Malformed(string name) => new(name, new MemoryStream([]));

    // -----------------------------------------------------------------------
    //  The four verdicts a consumer must be able to tell apart
    // -----------------------------------------------------------------------

    [Fact]
    public void OneMalformedAmongReadable_YieldsTheReadable_AndNamesTheRejected()
    {
        var streams = Fixtures();
        streams.Add(Malformed("malformed.xg"));
        var report = new SourceReport();

        var rows = NewIterator().IterateXgStreams(streams, report).ToList();

        rows.Should().NotBeEmpty("the readable fixtures still yield their decisions");
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
        var streams = Fixtures();
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
        streams.AddRange(Fixtures());
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
        var streams = Fixtures();
        streams.Add(Malformed("malformed.xg"));
        var report = new SourceReport();
        var iterator = NewIterator();

        _ = iterator.IterateXgStreams(streams, report).ToList();
        var attempted = report.AttemptedCount;

        var second = () => iterator.IterateXgStreams(Fixtures(), report).ToList();
        second.Should().Throw<InvalidOperationException>();

        report.AttemptedCount.Should().Be(attempted, "the completed report is a record of its walk");
        report.Rejected.Should().ContainSingle();
        report.IsComplete.Should().BeTrue();
    }

    [Fact]
    public void ReEnumeratingTheSameEnumerable_IsRefusedToo()
    {
        var report = new SourceReport();
        var enumerable = NewIterator().IterateXgStreams(Fixtures(), report);

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

        var first = iterator.IterateXgStreams(Fixtures(), report);
        var second = iterator.IterateXgStreams(Fixtures(), report);

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

        var withBad = Fixtures();
        withBad.Add(Malformed("malformed.xg"));
        _ = iterator.IterateXgStreamDiagrams(withBad, first).ToList();
        _ = iterator.IterateXgStreamDiagrams(Fixtures(), second).ToList();

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
            foreach (var src in Directory.GetFiles(FixtureDir, "*.xg"))
                File.Copy(src, Path.Combine(tempDir, Path.GetFileName(src)));
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
        var withBad = Fixtures();
        withBad.Add(Malformed("malformed.xg"));
        var withBadAgain = Fixtures();
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
        var spy = new ListLogger<FilteredDecisionIterator>();
        var iterator = new FilteredDecisionIterator(new DecisionFilterSet(), PlayRanking.Equity, spy);
        var report = new SourceReport();

        _ = iterator.IterateXgStreams([Malformed("malformed.xg")], report).ToList();

        var skip = spy.Entries.Should().ContainSingle(e => e.Message.Contains("malformed.xg")).Which;
        skip.Level.Should().Be(LogLevel.Warning);
        skip.Exception.Should().BeSameAs(report.Rejected[0].Reason);
    }

    // -----------------------------------------------------------------------
    //  The boundaries that are not rejections
    // -----------------------------------------------------------------------

    [Fact]
    public void AnInvalidSourceName_IsAUsageError_NotARejection()
    {
        var streams = Fixtures().Take(1).ToList();
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

        var act = () => NewIterator().IterateXgStreams(OneFixtureThenThrow(), report).ToList();
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

        var act = () => iterator.IterateXgStreams(Fixtures(), report).ToList();
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
            foreach (var _ in NewIterator().IterateXgStreams(Fixtures(), report))
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
        streams.AddRange(Fixtures());
        streams.Add(Malformed("two.xg"));
        IReadOnlyList<SourceRejection>? midWalk = null;

        foreach (var _ in NewIterator().IterateXgStreams(streams, report))
            midWalk ??= report.Rejected;

        midWalk.Should().NotBeNull("the fixtures yield at least one decision");
        midWalk!.Select(r => r.SourceName).Should().Equal("one.xg");
        report.Rejected.Select(r => r.SourceName).Should().Equal("one.xg", "two.xg");
        midWalk.Should().NotBeAssignableTo<List<SourceRejection>>();
        midWalk.Should().NotBeAssignableTo<SourceRejection[]>();
    }

    // -----------------------------------------------------------------------
    //  Doubles
    // -----------------------------------------------------------------------

    private static IEnumerable<XgFileStream> OneFixtureThenThrow()
    {
        yield return Fixtures()[0];
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

    private sealed class ListLogger<T> : ILogger<T>
    {
        public List<LogEntry> Entries { get; } = [];

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(
            LogLevel logLevel, EventId eventId, TState state,
            Exception? exception, Func<TState, Exception?, string> formatter)
        {
            Entries.Add(new LogEntry(logLevel, formatter(state, exception), exception));
        }
    }

    private sealed record LogEntry(LogLevel Level, string Message, Exception? Exception);
}
