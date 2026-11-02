using System.Collections.Immutable;
using ConvertXgToJson_Lib;

namespace XgFilter_Lib;

/// <summary>
/// What one <see cref="FilteredDecisionIterator"/> walk did with its sources,
/// as data: how many it attempted to read, which it rejected and why, whether
/// it reached the end (halheinrich/backgammon#368), and what it left out of
/// the sources it did read (halheinrich/backgammon#514). The caller
/// creates one, hands it to the walk entry point (a directory or stream
/// walk) it is about to enumerate, and
/// reads it during or after the enumeration; the iterator is its only
/// writer. A caller that passes none gets exactly the behaviour it always
/// had — a rejected source is logged and skipped, an omission is logged, and
/// the walk goes on.
///
/// <para>
/// <b>Two kinds of fact.</b> <see cref="AttemptedCount"/>,
/// <see cref="ReadableCount"/>, <see cref="Rejected"/>,
/// <see cref="IsComplete"/> and <see cref="AllRejected"/> are about
/// <em>decoding</em>: whether a source could be read at all.
/// <see cref="Omissions"/> and <see cref="ForcedPlayCount"/> are about
/// <em>content</em>: what the walk found inside the sources that decoded.
/// Neither kind bears on the other. A source with omissions, or with every
/// game omitted, is decoded: it counts as readable, never as rejected, and
/// <see cref="AllRejected"/> stays false for it. There is no third state.
/// </para>
///
/// <para>
/// <b>A report serves one walk.</b> It is claimed by the first
/// <c>MoveNext</c> of the enumeration it was handed to and refuses every
/// later claim with <see cref="InvalidOperationException"/> — a second
/// enumeration of the same enumerable, or the same report passed to another
/// call. So a completed report is a record of its walk and nothing can
/// clear, append to or replace it: a consumer that keeps a report beside the
/// parse it describes keeps it unchanged however many walks follow, and a
/// consumer that enumerates again makes a new one. The shape is the
/// producer's <c>XgIteratorState</c>: caller-supplied, populated by the
/// iterator, read-only to everyone else.
/// </para>
///
/// <para>
/// <b>What counts.</b> A source is <em>attempted</em> when the iterator
/// starts reading it; <em>readable</em> when that read decodes — when the
/// converter's read (<see cref="XgSourceReader"/>) returns
/// <see cref="XgSourceDecoded"/> — whether or not any of its decisions pass
/// the filters; and <em>rejected</em> when the read fails — when it returns
/// <see cref="XgSourceFailed"/>, the one outcome the iterator skips and logs,
/// and the only one this report records as a rejection. Each is recorded as
/// it happens, none derived from the others, so in a completed walk the
/// attempted sources are exactly the readable and the rejected ones. Every
/// failure below propagates out of the walk and leaves the report
/// incomplete, and none is a rejection; they differ in what they leave
/// recorded. A read interrupted before either outcome adds neither a
/// readable source nor a rejection: a cancellation (the attempt it ends is
/// counted), and a usage error (an invalid source name or a null stream,
/// refused before the attempt; a stream the read cannot decode in place,
/// refused by the read — <see cref="ArgumentException"/>). A later failure,
/// inside the producer, a filter or the consumer's loop, keeps the source
/// it interrupted readable, since that source was recorded when it decoded.
/// A failure enumerating the sources themselves records nothing more.
/// </para>
///
/// <para>
/// <b>Content covers what the walk examined, and no more.</b> At every
/// decision the walk yields, and once it has stopped however it stopped,
/// <see cref="Omissions"/> and <see cref="ForcedPlayCount"/> hold exactly what
/// the walk has examined so far, whether any decision passed the filters or
/// not, and the walk never reads on to finish them. A record it did not
/// examine says nothing: a match or game a filter's early exit skipped, the
/// rest of a game or match after one cut it short, the rest of a source a
/// consumer stopped in, a source never reached. So the decoding facts do not
/// depend on the filters and these do, and an empty list says the walk found
/// nothing, not that the sources are clean. Only a completed walk under an
/// empty filter set leaves nothing out of its reach, and even that walk ends
/// an <c>.xgp</c> at the one play it selects.
/// </para>
///
/// <para>
/// <b>Complete means the walk reached the end of its sources.</b> A consumer
/// that stopped early, or whose walk threw, holds a partial report:
/// <see cref="AttemptedCount"/>, <see cref="ReadableCount"/>,
/// <see cref="Rejected"/>, <see cref="Omissions"/> and
/// <see cref="ForcedPlayCount"/> are true of what was reached, and nothing is
/// claimed about the rest. The one
/// conclusion the report draws itself, <see cref="AllRejected"/>, is drawn
/// only from a completed walk over at least one source, so an empty
/// selection and a selection nobody finished reading are both told apart
/// from one whose every file was refused.
/// </para>
///
/// <para>
/// Not thread-safe: written by the enumerating thread, read by whoever holds
/// it, with no synchronization — the iterator is synchronous and so is this.
/// </para>
/// </summary>
public sealed class SourceReport
{
    private ImmutableList<SourceRejection> _rejected = [];
    private ImmutableList<XgOmission> _omissions = [];
    private bool _claimed;

    // How much of the current source's observer this report already holds,
    // so each update adds only what is new. Restarted per decoded source.
    private int _sourceOmissionsTaken;
    private int _sourceForcedPlaysTaken;

    /// <summary>
    /// The number of sources whose read was started, rejected ones included.
    /// Zero until the walk reaches its first source.
    /// </summary>
    public int AttemptedCount { get; private set; }

    /// <summary>
    /// The number of attempted sources that read successfully — whether or
    /// not any decision in them passed the filters. Recorded when a source's
    /// read decodes, before any of its decisions is filtered, so a readable
    /// source counts from the moment it is read, through a consumer that
    /// stops partway through it, and whatever the filters make of it. Never
    /// derived from the other counts: an attempt that ended in neither
    /// outcome — a cancellation, or a stream the read refused as a usage
    /// error — is attempted but neither readable nor rejected.
    /// </summary>
    public int ReadableCount { get; private set; }

    /// <summary>
    /// The sources that could not be read, in the order the walk reached
    /// them, each with the name the caller gave it and the exception its read
    /// raised. Immutable: the list handed out never changes, and a later read
    /// of this property after another rejection hands out a longer one.
    /// </summary>
    public IReadOnlyList<SourceRejection> Rejected => _rejected;

    /// <summary>
    /// What the walk has left out of the sources it decoded, in walk order:
    /// each omitted game and each omitted checker play, as the producer
    /// recorded it (<see cref="XgOmission"/>, whose
    /// <see cref="XgOmission.SourceFile"/> is the name the walk gave the
    /// source and whose <see cref="XgOmission.Reason"/> is text for a reader,
    /// which may hold a line break). About content, and covering only what
    /// the walk examined (see the class remarks). Immutable: the list handed
    /// out never changes, and a later read after another omission hands out a
    /// longer one.
    /// </summary>
    public IReadOnlyList<XgOmission> Omissions => _omissions;

    /// <summary>
    /// How many analysed checker plays of the sources the walk decoded it has
    /// passed by as forced, a dance included: the sum over those sources.
    /// Information, not a problem: a forced play is no decision and never an
    /// omission (<see cref="XgIteratorState.ForcedPlayCount"/>). About content,
    /// and covering only what the walk examined (see the class remarks).
    /// </summary>
    public int ForcedPlayCount { get; private set; }

    /// <summary>
    /// True once the walk has reached the end of its sources. False while it
    /// is in progress, after it stopped early, and after it threw.
    /// </summary>
    public bool IsComplete { get; private set; }

    /// <summary>
    /// True when a <em>completed</em> walk attempted at least one source and
    /// read none of them — the selection held nothing readable. Deliberately
    /// not vacuous: false for an empty selection (nothing was attempted) and
    /// false for a partial walk (nothing is concluded about sources not
    /// reached), so a consumer can say "no file could be read" only when that
    /// is what happened. About decoding alone: a source with every game
    /// omitted was read, so a walk of only such sources is not all rejected,
    /// and what it left out is on <see cref="Omissions"/>.
    /// </summary>
    public bool AllRejected => IsComplete && AttemptedCount > 0 && ReadableCount == 0;

    /// <summary>
    /// Claims this report for the walk that is starting. Called by the
    /// iterator on the enumeration's first <c>MoveNext</c>.
    /// </summary>
    /// <exception cref="InvalidOperationException">
    /// The report already belongs to a walk, finished or not.
    /// </exception>
    internal void Claim()
    {
        if (_claimed)
            throw new InvalidOperationException(
                "This SourceReport already belongs to an enumeration. A report serves " +
                "exactly one walk; create a new one for each enumeration.");

        _claimed = true;
    }

    /// <summary>Records that a source's read is starting.</summary>
    internal void Attempted() => AttemptedCount++;

    /// <summary>
    /// Records that the source just attempted was read: the converter's read
    /// decoded it. Called before its decisions are iterated. It also starts
    /// this source's <see cref="Observe"/> bookkeeping, since each decoded
    /// source has an observer of its own.
    /// </summary>
    internal void Decoded()
    {
        ReadableCount++;
        _sourceOmissionsTaken = 0;
        _sourceForcedPlaysTaken = 0;
    }

    /// <summary>
    /// Brings <see cref="Omissions"/> and <see cref="ForcedPlayCount"/> up to
    /// date with <paramref name="source"/>, the observer of the source decoded
    /// last, adding only what is new since the last call for that source, so
    /// the iterator may call this as often as the report must be current.
    /// Copies each new <see cref="XgOmission"/> across: the observer's own
    /// list grows during its enumeration and is never handed out from here.
    /// </summary>
    internal void Observe(XgIteratorState source)
    {
        var omissions = source.Omissions;
        if (omissions.Count > _sourceOmissionsTaken)
        {
            _omissions = _omissions.AddRange(omissions.Skip(_sourceOmissionsTaken));
            _sourceOmissionsTaken = omissions.Count;
        }

        ForcedPlayCount += source.ForcedPlayCount - _sourceForcedPlaysTaken;
        _sourceForcedPlaysTaken = source.ForcedPlayCount;
    }

    /// <summary>
    /// Records that the source just attempted could not be read: the
    /// converter's failed outcome, projected into a <see cref="SourceRejection"/>
    /// here and nowhere else, under the name the walk gave the source and with
    /// the exception that failed it.
    /// </summary>
    internal void Reject(string sourceName, XgSourceFailed failure) =>
        _rejected = _rejected.Add(new SourceRejection(sourceName, failure.Exception));

    /// <summary>Records that the walk reached the end of its sources.</summary>
    internal void Complete() => IsComplete = true;
}
