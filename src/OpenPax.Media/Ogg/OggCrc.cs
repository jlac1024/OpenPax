namespace OpenPax.Media.Ogg;

/// <summary>
/// Ogg page checksum: CRC-32 with polynomial 0x04C11DB7, not reflected,
/// initial value 0, computed with the checksum field read as zero.
/// </summary>
internal static class OggCrc
{
    private const int ChecksumOffset = 22;
    private static readonly uint[] _table = BuildTable();

    public static uint Compute(ReadOnlySpan<byte> page)
    {
        var crc = 0u;
        for (var i = 0; i < page.Length; i++)
        {
            var b = i is >= ChecksumOffset and < ChecksumOffset + 4 ? (byte)0 : page[i];
            crc = (crc << 8) ^ _table[((crc >> 24) ^ b) & 0xFF];
        }

        return crc;
    }

    private static uint[] BuildTable()
    {
        var table = new uint[256];
        for (var i = 0u; i < 256; i++)
        {
            var r = i << 24;
            for (var k = 0; k < 8; k++)
            {
                r = (r & 0x80000000) != 0 ? (r << 1) ^ 0x04C11DB7 : r << 1;
            }

            table[i] = r;
        }

        return table;
    }
}
