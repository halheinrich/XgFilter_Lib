using System.Collections.Immutable;

namespace XgFilter_Lib;

/// <summary>
/// What one <see cref="FilteredDecisionIterator"/> walk did with its sources,
/// as data: how many it attempted to read, which it rejected and why, and
/// whether it reached the end (halheinrich/backgammon#368). The caller
/// creates one, hands it to the entry point it is about to enumerate, and
/// reads it during or after the enumeration; the iterator is its only
/// writer. A caller that passes none gets exactly the behaviour it always
/// had — a rejected source is logged and skipped, and the walk goes on.
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
/// starts reading it, and <em>rejected</em> when that read throws — the one
/// boundary the iterator already skipped and logged, and the only one this
/// report records. A source that reads is readable whether or not any of its
/// decisions pass the filters. An invalid source name or null stream (a usage
/// error, <see cref="ArgumentException"/>), a failure enumerating the sources
/// themselves, and a failure inside a filter or the consumer's loop all
/// propagate exactly as before and are never a rejection; they leave the
/// report incomplete.
/// </para>
///
/// <para>
/// <b>Complete means the walk reached the end of its sources.</b> A consumer
/// that stopped early, or whose walk threw, holds a partial report:
/// <see cref="AttemptedCount"/> and <see cref="Rejected"/> are true of the
/// sources reached, and nothing is claimed about the rest. The one
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
    private bool _claimed;

    /// <summary>
    /// The number of sources whose read was started, rejected ones included.
    /// Zero until the walk reaches its first source.
    /// </summary>
    public int AttemptedCount { get; private set; }

    /// <summary>
    /// The number of attempted sources that read successfully — whether or
    /// not any decision in them passed the filters.
    /// </summary>
    public int ReadableCount => AttemptedCount - _rejected.Count;

    /// <summary>
    /// The sources that could not be read, in the order the walk reached
    /// them, each with the name the caller gave it and the exception its read
    /// raised. Immutable: the list handed out never changes, and a later read
    /// of this property after another rejection hands out a longer one.
    /// </summary>
    public IReadOnlyList<SourceRejection> Rejected => _rejected;

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
    /// is what happened.
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

    /// <summary>Records that the source just attempted could not be read.</summary>
    internal void Reject(string sourceName, Exception reason) =>
        _rejected = _rejected.Add(new SourceRejection(sourceName, reason));

    /// <summary>Records that the walk reached the end of its sources.</summary>
    internal void Complete() => IsComplete = true;
}
