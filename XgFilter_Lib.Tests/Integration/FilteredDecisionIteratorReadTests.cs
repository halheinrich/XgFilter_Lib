using BgDataTypes_Lib;
using ConvertXgToJson_Lib;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using XgFilter_Lib.Filtering;
using XgFilter_Lib.Tests.Helpers;

namespace XgFilter_Lib.Tests.Integration;

/// <summary>
/// How every <see cref="FilteredDecisionIterator"/> entry point reads its
/// sources: through the converter's one read of one source,
/// <see cref="XgSourceReader"/>, in the format the entry point states, with
/// the read's failure policy as the walk's — a failed source recorded and
/// skipped, a cancellation propagated, a usage error thrown. Every source is
/// synthesized (<see cref="SyntheticSources"/>), so these tests read nothing
/// from <c>TestData</c> and gate.
/// </summary>
public class FilteredDecisionIteratorReadTests
{
    private static FilteredDecisionIterator NewIterator(ILogger<FilteredDecisionIterator>? logger = null) =>
        new(new DecisionFilterSet(), PlayRanking.Equity, logger ?? NullLogger<FilteredDecisionIterator>.Instance);

    /// <summary>The two stream entry points, each projected to its decisions' identities.</summary>
    public static TheoryData<string> StreamShapes() =>
    [
        nameof(FilteredDecisionIterator.IterateXgStreams),
        nameof(FilteredDecisionIterator.IterateXgStreamDiagrams),
    ];

    private static IEnumerable<DecisionId> Walk(
        FilteredDecisionIterator iterator, string shape, IEnumerable<XgFileStream> entries, SourceReport report) =>
        shape switch
        {
            nameof(FilteredDecisionIterator.IterateXgStreams) =>
                iterator.IterateXgStreams(entries, report).Select(r => r.Id),
            nameof(FilteredDecisionIterator.IterateXgStreamDiagrams) =>
                iterator.IterateXgStreamDiagrams(entries, report).Select(d => d.Id),
            _ => throw new ArgumentOutOfRangeException(nameof(shape), shape, null),
        };

    // -----------------------------------------------------------------------
    //  A cancellation is the caller's, never a failed source
    // -----------------------------------------------------------------------

    [Theory]
    [MemberData(nameof(StreamShapes))]
    public void ACancellationDuringARead_Propagates_AndIsNotARejection(string shape)
    {
        // A readable source, a malformed one, then one whose read is
        // cancelled, then one the walk never reaches. The cancellation ends
        // the walk; what was reached before it is reported as it was.
        var file = SyntheticSources.TwoGameMatch();
        XgFileStream[] entries =
        [
            SyntheticSources.XgEntry("first.xg", file),
            SyntheticSources.MalformedEntry("malformed.xg"),
            new("cancelled.xg", new FailingStream(new OperationCanceledException("the caller cancelled"))),
            SyntheticSources.XgEntry("unreached.xg", file),
        ];
        var report = new SourceReport();
        var yielded = new List<DecisionId>();

        var act = () => yielded.AddRange(Walk(NewIterator(), shape, entries, report));

        act.Should().Throw<OperationCanceledException>().WithMessage("the caller cancelled");
        yielded.Should().Equal(SyntheticSources.ProducerIds(file, "first.xg"),
            "the source read before the cancellation still yielded its decisions");
        report.AttemptedCount.Should().Be(3, "the cancelled read was attempted; the source after it never was");
        report.ReadableCount.Should().Be(1, "a cancelled read is not a source that read, either");
        report.Rejected.Select(r => r.SourceName).Should().Equal(["malformed.xg"],
            "a cancelled read is not a source that failed to read");
        report.IsComplete.Should().BeFalse("a cancelled walk did not reach the end of its sources");
        report.AllRejected.Should().BeFalse();
    }

    // -----------------------------------------------------------------------
    //  A readable source is recorded when it reads, never derived
    // -----------------------------------------------------------------------

    [Theory]
    [MemberData(nameof(StreamShapes))]
    public void AReadableSource_CountsWhenItReads_WhateverTheFiltersMakeOfIt(string shape)
    {
        var file = SyntheticSources.TwoGameMatch();
        var iterator = new FilteredDecisionIterator(
            new DecisionFilterSet().Add(new MatchNothing()), PlayRanking.Equity,
            NullLogger<FilteredDecisionIterator>.Instance);
        var report = new SourceReport();

        Walk(iterator, shape, [SyntheticSources.XgEntry("one.xg", file), SyntheticSources.XgEntry("two.xg", file)], report)
            .Should().BeEmpty();

        report.AttemptedCount.Should().Be(2);
        report.ReadableCount.Should().Be(2, "no decision matching is the filters' verdict, not the file's");
        report.Rejected.Should().BeEmpty();
        report.IsComplete.Should().BeTrue();
        report.AllRejected.Should().BeFalse();
    }

    [Theory]
    [MemberData(nameof(StreamShapes))]
    public void AReadableSource_CountsBeforeItsDecisionsAreFiltered(string shape)
    {
        // A consumer that stops on the first decision has read the source it
        // came from: the report says so mid-source, not only once the
        // source's decisions run out.
        var file = SyntheticSources.TwoGameMatch();
        var report = new SourceReport();

        _ = Walk(NewIterator(), shape, [SyntheticSources.XgEntry("one.xg", file), SyntheticSources.XgEntry("two.xg", file)], report)
            .First();

        report.AttemptedCount.Should().Be(1);
        report.ReadableCount.Should().Be(1);
        report.IsComplete.Should().BeFalse();
    }

    // -----------------------------------------------------------------------
    //  A failed source carries the exception that failed its read
    // -----------------------------------------------------------------------

    [Theory]
    [MemberData(nameof(StreamShapes))]
    public void AFailedSource_IsRecordedWithTheExceptionThatFailedItsRead(string shape)
    {
        // The read's failure policy fails the source with the stream's own
        // exception; the rejection carries that instance, and so does the
        // skip warning — one fact, recorded once, traced once.
        var failure = new IOException("the disk failed");
        var spy = new ListLogger<FilteredDecisionIterator>();
        var report = new SourceReport();

        Walk(NewIterator(spy), shape, [new XgFileStream("failing.xg", new FailingStream(failure))], report)
            .Should().BeEmpty();

        var rejection = report.Rejected.Should().ContainSingle().Which;
        rejection.SourceName.Should().Be("failing.xg");
        rejection.Reason.Should().BeSameAs(failure);
        spy.Entries.Should().ContainSingle(e => e.Message == "Skipping failing.xg")
            .Which.Exception.Should().BeSameAs(failure);
        report.AttemptedCount.Should().Be(1);
        report.IsComplete.Should().BeTrue();
        report.AllRejected.Should().BeTrue();
    }

    // -----------------------------------------------------------------------
    //  The format each entry point states
    // -----------------------------------------------------------------------

    [Fact]
    public void TheJsonDirectoryWalk_DecodesJson()
    {
        // The JSON walk states the JSON format: its files decode as the
        // library's JSON document, with each decision's identity the file's
        // name, extension and all.
        var file = SyntheticSources.TwoGameMatch();
        var dir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(dir);
        try
        {
            File.WriteAllBytes(Path.Combine(dir, "match.json"), SyntheticSources.JsonBytes(file));
            var report = new SourceReport();

            var ids = NewIterator().IterateJsonDirectory(dir, report).Select(r => r.Id).ToList();

            ids.Should().NotBeEmpty().And.Equal(SyntheticSources.ProducerIds(file, "match.json"));
            report.Rejected.Should().BeEmpty();
            report.AttemptedCount.Should().Be(1);
            report.IsComplete.Should().BeTrue();
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }

    // -----------------------------------------------------------------------
    //  The stream entry's precondition is the converter's, and a usage error
    // -----------------------------------------------------------------------

    /// <summary>
    /// Stream entries that hold a well-formed file the read cannot decode in
    /// place: not seekable, not at position 0, and disposed (not readable).
    /// </summary>
    public static TheoryData<string> MisusedStreams() => ["not seekable", "not at position 0", "disposed"];

    private static Stream Misused(string misuse, byte[] bytes)
    {
        switch (misuse)
        {
            case "not seekable":
                return new NonSeekableStream(bytes);
            case "not at position 0":
                var advanced = new MemoryStream(bytes);
                advanced.Position = 1;
                return advanced;
            case "disposed":
                var disposed = new MemoryStream(bytes);
                disposed.Dispose();
                return disposed;
            default:
                throw new ArgumentOutOfRangeException(nameof(misuse), misuse, null);
        }
    }

    [Theory]
    [MemberData(nameof(MisusedStreams))]
    public void AStreamTheReadCannotDecodeInPlace_IsAUsageError_NotARejection(string misuse)
    {
        var file = SyntheticSources.TwoGameMatch();
        XgFileStream[] entries =
        [
            SyntheticSources.XgEntry("first.xg", file),
            new("misused.xg", Misused(misuse, SyntheticSources.XgBytes(file))),
        ];
        var report = new SourceReport();

        var act = () => NewIterator().IterateXgStreams(entries, report).ToList();

        act.Should().Throw<ArgumentException>();
        report.Rejected.Should().BeEmpty("a stream the caller handed over unusable is a bug, not an unreadable file");
        report.AttemptedCount.Should().Be(2, "the read was attempted, and refused how it was called");
        report.ReadableCount.Should().Be(1, "the refused stream did not read");
        report.IsComplete.Should().BeFalse();
    }

    [Fact]
    public void AMalformedSource_IsStillARejection()
    {
        var file = SyntheticSources.TwoGameMatch();
        var report = new SourceReport();

        var ids = NewIterator()
            .IterateXgStreams([SyntheticSources.MalformedEntry("malformed.xg"), SyntheticSources.XgEntry("good.xg", file)], report)
            .Select(r => r.Id)
            .ToList();

        ids.Should().Equal(SyntheticSources.ProducerIds(file, "good.xg"));
        report.Rejected.Should().ContainSingle().Which.SourceName.Should().Be("malformed.xg");
        report.AttemptedCount.Should().Be(2);
        report.IsComplete.Should().BeTrue();
    }

    // -----------------------------------------------------------------------
    //  Doubles
    // -----------------------------------------------------------------------

    private sealed class MatchNothing : IDecisionFilter
    {
        public bool Matches(IDecisionFilterData data) => false;
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
