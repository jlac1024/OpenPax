using System.Security.Cryptography;
using System.Text;
using OpenPax.Data.Compression;

namespace OpenPax.Data.Tests;

/// <summary>The LZMA and LZMA2 decoders, on streams packed by another implementation (liblzma).</summary>
public class LzmaTests
{
    // 5503 bytes: "OpenPax N, " 400 times, then 0 to 255 three times.
    private const string Expected = "59a9d30a58eff0fea6aeca6d6d06d228009337dc0821357104e9fcb71bf932cb";

    private const string Lzma1 = "XQAAAQAAJ5wIpw/NKA+KJMmyCk3MAQVZv1PTPYUNc/NYITz6KaOsvkhu7thzFICxehRav13PKIrfg8/oyJpeGPrv/O+K0dVd2+4kFpEDI0IOOByPUtuBOdNrhugsHo6YsChHS75qRusCGYEnSmeCQyZityDAPDfkI2rKLguQOULRyGn66OIBqrjnM6pfCxNFIEWPHVJfH6rbm861DPCSVYCuixEmzVrHv+wT/PjuafMgv0pkNquhjYH1VTP5LFcpbVZ831+P3QX8G+c85FivoNUoiSI0Qi0acNkjKYObPO8FYVzOta+WAjbyoOoRIJJ58PHoK2BfKfeekyWmQvZbRSLoM71BGpg+GnlllbUDYDDBAO+OMhqQPP0Hd225g07fg2sA33kvRn37EXSx5ohCw+f6mgY2CQs2tnFv+06p8KxQFqJ8qJJz3sHDlxo4EsRHVyFGL/L6EHHaavQUZdaCVeLReV/9/Jah1mffqSla8koCZ2kVAXYEevk1hzKrJSA3dTSTAMjNN9kQOGqGeYXMmSZcgMoaZ2SfPxOLQ/BtZzo/d7MKYBs5FuV8uSZIeBoHGFKKyM7i384F9iqVFBEadhe1Rb1z/SZiYoyhU7EF36TYCPEPzBMTERjf//vpWYw=";

    private const string Lzma2 = "CuAVfgHUXQAnnAinD80oD4okybIKTcwBBVm/U9M9hQ1z81ghPPopo6y+SG7u2HMUgLF6FFq/Xc8oit+Dz+jIml4Y+u/874rR1V3b7iQWkQMjQg44HI9S24E502uG6CwejpiwKEdLvmpG6wIZgSdKZ4JDJmK3IMA8N+QjasouC5A5QtHIafro4gGquOczql8LE0UgRY8dUl8fqtubzrUM8JJVgK6LESbNWse/7BP8+O5p8yC/SmQ2q6GNgfVVM/ksVyltVnzfX4/dBfwb5zzkWK+g1SiJIjRCLRpw2SMpg5s87wVhXM61r5YCNvKg6hEgknnw8egrYF8p956TJaZC9ltFIugzvUEamD4aeWWVtQNgMMEA744yGpA8/Qd3bbmDTt+DawDfeS9GffsRdLHmiELD5/qaBjYJCza2cW/7TqnwrFAWonyoknPewcOXGjgSxEdXIUYv8voQcdpq9BRl1oJV4tF5X/38lqHWZ9+pKVrySgJnaRUBdgR6+TWHMqslIDd1NJMAyM032RA4aoZ5hcyZJlyAyhpnZJ8/E4tD8G1nOj93swpgGzkW5Xy5Jkh4GgcYUorIzuLfzgX2KpUUERp2F7VFvXP9JmJijKFTsQXfpNgI8Q/MEv8s4AAA";

    private static string Sha256(byte[] data) => Convert.ToHexStringLower(SHA256.HashData(data));

    private static byte[] ReadAll(Stream stream, int step)
    {
        var output = new MemoryStream();
        var buffer = new byte[step];
        int n;
        while ((n = stream.Read(buffer, 0, buffer.Length)) > 0)
        {
            output.Write(buffer, 0, n);
        }

        return output.ToArray();
    }

    [Theory]
    [InlineData(1)]
    [InlineData(7)]
    [InlineData(65536)]
    public void LzmaWithEndMarkDecodes(int step)
    {
        var stream = new LzmaStream(new MemoryStream(Convert.FromBase64String(Lzma1)));
        var data = ReadAll(stream, step);
        Assert.Equal(5503, data.Length);
        Assert.Equal(Expected, Sha256(data));
        Assert.StartsWith("OpenPax 0, OpenPax 1, OpenPax 4, ", Encoding.ASCII.GetString(data));
    }

    [Theory]
    [InlineData(1)]
    [InlineData(13)]
    [InlineData(65536)]
    public void Lzma2Decodes(int step)
    {
        var stream = new Lzma2Stream(new MemoryStream(Convert.FromBase64String(Lzma2)), 5503);
        var data = ReadAll(stream, step);
        Assert.Equal(Expected, Sha256(data));
    }

    [Fact]
    public void DamagedDataIsRefused()
    {
        var packed = Convert.FromBase64String(Lzma1);
        packed[^40] ^= 0x5A;
        packed[^60] ^= 0x33;
        var stream = new LzmaStream(new MemoryStream(packed));
        var ex = Record.Exception(() => ReadAll(stream, 4096));
        if (ex is null)
        {
            // A flipped bit can still decode to something: just not the same bytes.
            Assert.NotEqual(Expected, Sha256(ReadAll(new LzmaStream(new MemoryStream(packed)), 4096)));
        }
        else
        {
            Assert.IsType<InvalidDataException>(ex);
        }
    }

    [Fact]
    public void TruncatedDataIsRefused()
    {
        var packed = Convert.FromBase64String(Lzma2)[..200];
        var stream = new Lzma2Stream(new MemoryStream(packed), 5503);
        Assert.Throws<InvalidDataException>(() => ReadAll(stream, 4096));
    }
}
