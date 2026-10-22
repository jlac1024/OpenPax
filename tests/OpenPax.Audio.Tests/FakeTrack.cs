namespace OpenPax.Audio.Tests;

internal sealed class FakeTrack(int frames) : IMusicStream
{
    private int _left = frames;

    public int Read(Span<short> stereo)
    {
        var n = Math.Min(_left, stereo.Length / 2);
        stereo[..(n * 2)].Fill(100);
        _left -= n;
        return n;
    }
}
