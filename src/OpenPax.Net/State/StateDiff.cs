using System.IO.Compression;

namespace OpenPax.Net.State;

/// <summary>
/// A state against the last one a client was sent: each byte XORed with
/// the byte at the same place before (nothing past the old end), then
/// compressed. Most of a game is unchanged from one update to the next, so
/// this is a few kilobytes; a whole state is the same with nothing before.
/// </summary>
public static class StateDiff
{
    public static byte[] Encode(ReadOnlySpan<byte> state, ReadOnlySpan<byte> before)
    {
        var mixed = state.ToArray();
        var common = Math.Min(mixed.Length, before.Length);
        for (var i = 0; i < common; i++)
        {
            mixed[i] ^= before[i];
        }

        using var output = new MemoryStream();
        using (var deflate = new DeflateStream(output, CompressionLevel.Fastest, leaveOpen: true))
        {
            deflate.Write(mixed);
        }

        return output.ToArray();
    }

    /// <summary>The state again from the payload and the same earlier state; length is the state's own.</summary>
    public static byte[] Decode(byte[] payload, int length, ReadOnlySpan<byte> before)
    {
        if (length < 0 || length > NetProtocol.MaxMessage)
        {
            throw new InvalidDataException(NetText.MessageTooLarge);
        }

        var state = new byte[length];
        using (var inflate = new DeflateStream(new MemoryStream(payload), CompressionMode.Decompress))
        {
            inflate.ReadExactly(state);
        }

        var common = Math.Min(length, before.Length);
        for (var i = 0; i < common; i++)
        {
            state[i] ^= before[i];
        }

        return state;
    }
}
