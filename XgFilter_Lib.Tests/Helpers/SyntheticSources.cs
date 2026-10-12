using System.Text;
using BgDataTypes_Lib;
using ConvertXgToJson_Lib;
using ConvertXgToJson_Lib.Models;

namespace XgFilter_Lib.Tests.Helpers;

/// <summary>
/// Sources synthesized through the producer's public builder
/// (<see cref="XgFileBuilder"/>) and written as the bytes a walk reads — XG's
/// binary container (<see cref="XgFileWriter"/>) or the library's JSON
/// document (<see cref="XgFileReader.ToJson"/>) — so the tests that use them
/// read nothing from <c>TestData</c> and gate. Plus the stream doubles a
/// read's failure, cancellation and usage-error paths need.
/// </summary>
internal static class SyntheticSources
{
    /// <summary>
    /// Six legal plays from the standard opening, alternating player 1 and
    /// player 2 and each in the mover's own numbering. No play touches a
    /// point the other side holds, so the sequence is legal from the opening
    /// whichever player moves first, and any prefix of it is a game.
    /// </summary>
    private static readonly (DiceRoll Dice, Play Played)[] OpeningMoves =
    [
        (new DiceRoll(3, 1), [new(8, 5), new(6, 5)]),
        (new DiceRoll(3, 1), [new(8, 5), new(6, 5)]),
        (new DiceRoll(4, 2), [new(8, 4), new(6, 4)]),
        (new DiceRoll(4, 2), [new(8, 4), new(6, 4)]),
        (new DiceRoll(6, 1), [new(13, 7), new(8, 7)]),
        (new DiceRoll(6, 1), [new(13, 7), new(8, 7)]),
    ];

    /// <summary>The most moves <see cref="WithMoves"/> can play from the opening.</summary>
    public static int MaxMoves => OpeningMoves.Length;

    /// <summary>
    /// Plays the first <paramref name="moves"/> of the opening sequence into
    /// <paramref name="game"/>, player 1 first, each an analysed decision — so
    /// the game yields decisions numbered 1 to <paramref name="moves"/>.
    /// </summary>
    public static XgGameBuilder WithMoves(this XgGameBuilder game, int moves)
    {
        ArgumentOutOfRangeException.ThrowIfGreaterThan(moves, MaxMoves);
        for (int i = 0; i < moves; i++)
        {
            var player = i % 2 == 0 ? XgPlayer.Player1 : XgPlayer.Player2;
            game.Play(player, OpeningMoves[i].Dice, OpeningMoves[i].Played);
        }
        return game;
    }

    /// <summary>
    /// A 5-point match between <paramref name="player1"/> and
    /// <paramref name="player2"/>: two games of <paramref name="moves"/>
    /// decisions each, the first at 0-0 and the second at 1-0.
    /// </summary>
    public static XgFile TwoGameMatch(string player1 = "P1", string player2 = "P2", int moves = 4)
    {
        var builder = XgFileBuilder.ForMatch(5, player1, player2);
        builder.AddGame().WithMoves(moves);
        builder.AddGame(score1: 1, score2: 0).WithMoves(moves);
        return builder.Build();
    }

    /// <summary>The analysed-cube equities the synthesized cube decisions share; nothing here reads them.</summary>
    public static readonly XgCubeEquities Equities = new(NoDouble: 0.30, DoubleTake: 0.45, DoubleDrop: 1.0);

    /// <summary>
    /// A position as XG saves one to an <c>.xgp</c>: one game of a 5-point
    /// match at player 1 5-away, player 2 4-away, holding player 1's analysed
    /// cube decision and then, after the roll, player 1's analysed checker
    /// play — the cube record first, as XG writes them. Read under an
    /// <c>.xgp</c> name the producer yields the play alone (its single-decision
    /// policy); under an <c>.xg</c> name, both.
    /// </summary>
    public static XgFile Position()
    {
        var builder = XgFileBuilder.ForMatch(5, "P1", "P2");
        builder.AddGame(score1: 0, score2: 1)
            .CubeDecision(XgPlayer.Player1, Equities, doublerAction: CubeAction.NoDouble)
            .Play(XgPlayer.Player1, OpeningMoves[0].Dice, OpeningMoves[0].Played,
                [new XgPlayCandidate(OpeningMoves[0].Played, equity: 0.1, ply: 2)]);
        return builder.Build();
    }

    /// <summary><paramref name="file"/> as XG's binary container.</summary>
    public static byte[] XgBytes(XgFile file) => XgFileWriter.ToBytes(file);

    /// <summary><paramref name="file"/> as the library's JSON document, UTF-8.</summary>
    public static byte[] JsonBytes(XgFile file) => Encoding.UTF8.GetBytes(XgFileReader.ToJson(file));

    /// <summary>A stream entry holding <paramref name="file"/>'s XG bytes under <paramref name="name"/>.</summary>
    public static XgFileStream XgEntry(string name, XgFile file) =>
        new(name, new MemoryStream(XgBytes(file)));

    /// <summary>A stream entry holding <paramref name="file"/>'s JSON document under <paramref name="name"/>.</summary>
    public static XgFileStream JsonEntry(string name, XgFile file) =>
        new(name, new MemoryStream(JsonBytes(file)));

    /// <summary>A stream entry the producer refuses to decode: zero bytes, no header.</summary>
    public static XgFileStream MalformedEntry(string name) => new(name, new MemoryStream([]));

    /// <summary>
    /// The decisions the producer yields for <paramref name="file"/> read
    /// under <paramref name="name"/> with no callbacks — what a walk's
    /// filtering is measured against.
    /// </summary>
    public static IReadOnlyList<DecisionId> ProducerIds(XgFile file, string name) =>
        [.. XgDecisionIterator.IterateDiagramRequests(file, name).Select(d => d.Id)];
}

/// <summary>
/// A readable stream over <paramref name="bytes"/> that cannot seek — what a
/// network or upload stream looks like to a reader.
/// </summary>
internal sealed class NonSeekableStream(byte[] bytes) : Stream
{
    private readonly MemoryStream _inner = new(bytes);

    public override bool CanRead => true;
    public override bool CanSeek => false;
    public override bool CanWrite => false;
    public override long Length => throw new NotSupportedException();
    public override long Position
    {
        get => throw new NotSupportedException();
        set => throw new NotSupportedException();
    }

    public override int Read(byte[] buffer, int offset, int count) => _inner.Read(buffer, offset, count);
    public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
    public override void Flush() { }
    public override void SetLength(long value) => throw new NotSupportedException();
    public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
}

/// <summary>
/// A readable, seekable stream at position 0 — so a read accepts it — whose
/// every read raises <paramref name="exception"/>, the same instance each
/// time: a disk that fails, or a read that is cancelled.
/// </summary>
internal sealed class FailingStream(Exception exception) : Stream
{
    public override bool CanRead => true;
    public override bool CanSeek => true;
    public override bool CanWrite => false;
    public override long Length => 1024;
    public override long Position { get; set; }

    public override int Read(byte[] buffer, int offset, int count) => throw exception;
    public override long Seek(long offset, SeekOrigin origin) => throw exception;
    public override void Flush() { }
    public override void SetLength(long value) => throw new NotSupportedException();
    public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
}
