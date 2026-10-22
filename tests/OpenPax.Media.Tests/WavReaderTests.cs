using System.Buffers.Binary;
using OpenPax.Media.Wav;

namespace OpenPax.Media.Tests;

public class WavReaderTests
{
    [Fact]
    public void Reads16BitStereo()
    {
        var file = Build(rate: 22050, channels: 2, bits: 16, data: [0x01, 0x00, 0xFF, 0xFF, 0x00, 0x80, 0xFF, 0x7F]);

        var sound = WavReader.Read(file);

        Assert.Equal(22050, sound.SampleRate);
        Assert.Equal(2, sound.Frames);
        Assert.Equal([1, -1, short.MinValue, short.MaxValue], sound.ToInt16());
    }

    [Fact]
    public void Reads8BitAsUnsigned()
    {
        var sound = WavReader.Read(Build(rate: 22254, channels: 1, bits: 8, data: [128, 0, 255]));

        Assert.Equal([0, -32768, 127 << 8], sound.ToInt16());
    }

    [Fact]
    public void IgnoresBytesAfterTheRiffChunk()
    {
        // Six GOG sounds end with two such bytes.
        byte[] file = [.. Build(rate: 22050, channels: 1, bits: 16, data: [1, 0]), 0, 0];

        Assert.Equal(1, WavReader.Read(file).Frames);
    }

    [Fact]
    public void SkipsUnknownChunks()
    {
        var plain = Build(rate: 22050, channels: 1, bits: 16, data: [5, 0]);
        byte[] list = [.. "LIST"u8, 3, 0, 0, 0, 1, 2, 3, 0];
        byte[] file = [.. plain, .. list];
        BinaryPrimitives.WriteUInt32LittleEndian(file.AsSpan(4), (uint)(file.Length - 8));

        Assert.Equal([5], WavReader.Read(file).ToInt16());
    }

    [Fact]
    public void RefusesCompressedFormats()
    {
        var file = Build(rate: 22050, channels: 1, bits: 4, data: [0]);
        file[20] = 2; // ADPCM
        Assert.Throws<InvalidDataException>(() => WavReader.Read(file));
    }

    [Fact]
    public void EveryGameSoundReads()
    {
        var folder = GameData.RequireFolder();
        var files = Directory.EnumerateFiles(Path.Combine(folder, "Sound"), "*", SearchOption.AllDirectories)
            .Where(p => p.EndsWith(".wav", StringComparison.OrdinalIgnoreCase))
            .ToList();

        Assert.Equal(204, files.Count);
        Assert.All(files, p => Assert.True(WavReader.Read(File.ReadAllBytes(p)).Frames > 0, p));
    }

    private static byte[] Build(int rate, int channels, int bits, byte[] data)
    {
        var file = new byte[44 + data.Length];
        "RIFF"u8.CopyTo(file);
        BinaryPrimitives.WriteUInt32LittleEndian(file.AsSpan(4), (uint)(36 + data.Length));
        "WAVEfmt "u8.CopyTo(file.AsSpan(8));
        BinaryPrimitives.WriteUInt32LittleEndian(file.AsSpan(16), 16);
        BinaryPrimitives.WriteUInt16LittleEndian(file.AsSpan(20), 1);
        BinaryPrimitives.WriteUInt16LittleEndian(file.AsSpan(22), (ushort)channels);
        BinaryPrimitives.WriteInt32LittleEndian(file.AsSpan(24), rate);
        BinaryPrimitives.WriteInt32LittleEndian(file.AsSpan(28), rate * channels * bits / 8);
        BinaryPrimitives.WriteUInt16LittleEndian(file.AsSpan(32), (ushort)(channels * bits / 8));
        BinaryPrimitives.WriteUInt16LittleEndian(file.AsSpan(34), (ushort)bits);
        "data"u8.CopyTo(file.AsSpan(36));
        BinaryPrimitives.WriteUInt32LittleEndian(file.AsSpan(40), (uint)data.Length);
        data.CopyTo(file, 44);
        return file;
    }
}
