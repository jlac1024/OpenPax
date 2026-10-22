using System.Buffers.Binary;

namespace OpenPax.Net.Wire;

/// <summary>
/// Messages on a stream: a 4 byte little endian length of what follows, the
/// type byte, then the body. TCP keeps them whole and in order.
/// </summary>
public static class Frames
{
    public static void Write(Stream stream, MessageType type, byte[] body)
    {
        var frame = new byte[5 + body.Length];
        BinaryPrimitives.WriteInt32LittleEndian(frame, body.Length + 1);
        frame[4] = (byte)type;
        body.CopyTo(frame, 5);
        stream.Write(frame);
        stream.Flush();
    }

    /// <summary>The next message, or null when the stream has ended cleanly.</summary>
    public static (MessageType Type, byte[] Body)? Read(Stream stream)
    {
        Span<byte> head = stackalloc byte[4];
        if (stream.ReadAtLeast(head, 4, throwOnEndOfStream: false) < 4)
        {
            return null;
        }

        var length = BinaryPrimitives.ReadInt32LittleEndian(head);
        if (length < 1 || length > NetProtocol.MaxMessage)
        {
            throw new InvalidDataException(NetText.MessageTooLarge);
        }

        var frame = new byte[length];
        stream.ReadExactly(frame);
        return ((MessageType)frame[0], frame[1..]);
    }
}
