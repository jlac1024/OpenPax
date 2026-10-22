using OpenPax.Net.Messages;
using OpenPax.Net.State;
using OpenPax.Net.Wire;
using OpenPax.Sim;

namespace OpenPax.Net.Tests;

public class WireTests
{
    [Fact]
    public void MessagesComeBackAsSent()
    {
        var state = new StateMessage(12, 0xDEADBEEFUL, true, 3, [1, 2, 3], StepEvents.TradeOffered, [new NoticeData(NoticeKind.PlayerEliminated, 1, 2, 3, 4, 5, 6, true), new NoticeData(NoticeKind.ModMessage, 0, 0, -1, -1, -1, 0, false) { Text = "A mod speaks" }]);
        NetMessage[] messages = [new HelloMessage(1, "Name é", "mods"), new WelcomeMessage(2), new RefusedMessage("no"), new CommandMessage([9, 8]), state, new SnapshotRequestMessage(), new ChatMessage(1, "hi")];
        using var stream = new MemoryStream();
        foreach (var m in messages)
        {
            var (type, body) = MessageCodec.Encode(m);
            Frames.Write(stream, type, body);
        }

        stream.Position = 0;
        foreach (var m in messages)
        {
            var frame = Frames.Read(stream);
            Assert.NotNull(frame);
            var back = MessageCodec.Decode(frame.Value.Type, frame.Value.Body);
            if (back is StateMessage s)
            {
                Assert.Equal(state.Payload, s.Payload);
                Assert.Equal(state.Notices, s.Notices);
                Assert.Equal(state with { Payload = s.Payload, Notices = s.Notices }, s);
            }
            else if (back is CommandMessage c)
            {
                Assert.Equal([9, 8], c.Command);
            }
            else
            {
                Assert.Equal(m, back);
            }
        }

        Assert.Null(Frames.Read(stream));
    }

    [Fact]
    public void AStateComesBackFromItsDifference()
    {
        var before = new byte[5000];
        new Random(1).NextBytes(before);
        var after = before.Concat(new byte[] { 1, 2, 3 }).ToArray();
        after[100] ^= 0xFF;
        var payload = StateDiff.Encode(after, before);
        Assert.True(payload.Length < 200);
        Assert.Equal(after, StateDiff.Decode(payload, after.Length, before));

        var shorter = before[..4000];
        Assert.Equal(shorter, StateDiff.Decode(StateDiff.Encode(shorter, before), shorter.Length, before));
        Assert.Equal(before, StateDiff.Decode(StateDiff.Encode(before, []), before.Length, []));
    }

    [Fact]
    public void AnOversizedFrameIsRefused()
    {
        using var stream = new MemoryStream([0xFF, 0xFF, 0xFF, 0x7F, 1]);
        Assert.Throws<InvalidDataException>(() => Frames.Read(stream));
    }
}
