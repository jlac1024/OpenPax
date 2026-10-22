namespace OpenPax.Media.Wav;

/// <summary>
/// Uncompressed audio as stored: interleaved samples, 8-bit unsigned or
/// 16-bit signed little endian, as in WAV files.
/// </summary>
public sealed record PcmSound(int SampleRate, int Channels, int BitsPerSample, byte[] Data)
{
    public int BytesPerFrame => Channels * BitsPerSample / 8;

    public int Frames => Data.Length / BytesPerFrame;

    /// <summary>Samples as signed 16-bit, interleaved, whatever the stored width.</summary>
    public short[] ToInt16()
    {
        var count = Frames * Channels;
        var samples = new short[count];
        if (BitsPerSample == 8)
        {
            for (var i = 0; i < count; i++)
            {
                // 8-bit WAV is unsigned with 128 as silence.
                samples[i] = (short)((Data[i] - 128) << 8);
            }
        }
        else
        {
            for (var i = 0; i < count; i++)
            {
                samples[i] = (short)(Data[2 * i] | Data[2 * i + 1] << 8);
            }
        }

        return samples;
    }
}
