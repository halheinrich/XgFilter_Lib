namespace XgFilter_Lib;

/// <summary>
/// One source a <see cref="FilteredDecisionIterator"/> walk could not read:
/// the name the caller gave it and the exception the read raised. Recorded
/// on a <see cref="SourceReport"/> at the moment the iterator skips the
/// source and continues (halheinrich/backgammon#368).
///
/// <para>
/// <see cref="Reason"/> is the read's own exception, carried as the value it
/// is rather than flattened to a sentence: its type says what kind of
/// failure it was (the producer's <see cref="InvalidDataException"/> for a
/// payload it refuses, an <see cref="IOException"/> for a file that could not
/// be opened, a <see cref="System.Text.Json.JsonException"/> for a JSON source
/// that does not parse) and its message names the check that failed, so a
/// consumer can show it, log it, or branch on it without parsing any text.
/// It is the same exception the iterator's skip warning carries.
/// </para>
/// </summary>
public sealed record SourceRejection
{
    /// <summary>
    /// Records that <paramref name="sourceName"/> was rejected for
    /// <paramref name="reason"/>.
    /// </summary>
    /// <exception cref="ArgumentException"><paramref name="sourceName"/> is null or blank.</exception>
    /// <exception cref="ArgumentNullException"><paramref name="reason"/> is null.</exception>
    public SourceRejection(string sourceName, Exception reason)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sourceName);
        ArgumentNullException.ThrowIfNull(reason);

        SourceName = sourceName;
        Reason = reason;
    }

    /// <summary>
    /// The source's name exactly as the caller supplied it — the directory
    /// walk's file name (extension included), or the
    /// <see cref="XgFileStream.FileName"/> of a stream entry — so a consumer
    /// can match it back to the file it picked.
    /// </summary>
    public string SourceName { get; }

    /// <summary>The exception the read raised; never null.</summary>
    public Exception Reason { get; }
}
