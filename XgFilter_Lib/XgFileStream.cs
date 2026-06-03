namespace XgFilter_Lib;

/// <summary>
/// A single named XG-format source supplied as an open <see cref="Stream"/>
/// rather than a filesystem path — the input shape for
/// <see cref="FilteredDecisionIterator.IterateXgStreams"/> and
/// <see cref="FilteredDecisionIterator.IterateXgStreamDiagrams"/>. Used when
/// the bytes never touch a server directory (e.g. a browser-picked file in a
/// WASM client): the caller pairs each stream with the original file name.
///
/// <para>
/// <b>FileName must carry the extension.</b> The producer's
/// <c>DecisionId</c> stamping discriminates <c>.xg</c> / <c>.xgp</c> /
/// <c>.json</c> via <c>Path.GetExtension</c> on the source name, so
/// <see cref="FileName"/> must be the original file name <i>with</i> its
/// extension (e.g. <c>"match.xg"</c>, not <c>"match"</c>). A null, blank, or
/// extension-less name is a usage error and is rejected when the iterator
/// reaches it.
/// </para>
///
/// <para>
/// <b>Stream ownership and lifetime.</b> The iterator reads each
/// <see cref="Data"/> stream exactly once, forward, from its current
/// position — which must be the start of the file (a fresh
/// <c>new MemoryStream(bytes)</c> satisfies this). The caller owns the stream
/// and is responsible for disposing it; the iterator does not close it. Because
/// the iterator methods are lazy (<c>yield</c>-based), the read happens during
/// enumeration, not at the call site: the caller must keep every stream open
/// and unread until enumeration reaches it. If that lifetime cannot be
/// guaranteed, buffer the bytes first (<c>new MemoryStream(File.ReadAllBytes(...))</c>
/// or <c>ReadOnlyMemory&lt;byte&gt;</c>-backed) so the stream cannot be
/// disposed out from under the deferred read.
/// </para>
/// </summary>
/// <param name="FileName">
/// The original file name, including its extension (<c>.xg</c>, <c>.xgp</c>,
/// or <c>.json</c>). Passed straight through to the producer as the decision's
/// <c>SourceFile</c>; never stripped.
/// </param>
/// <param name="Data">
/// An open, forward-readable stream positioned at the start of the file's
/// bytes. Read once during enumeration; owned and disposed by the caller.
/// </param>
public readonly record struct XgFileStream(string FileName, Stream Data);
