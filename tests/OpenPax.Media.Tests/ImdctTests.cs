using OpenPax.Media.Vorbis;

namespace OpenPax.Media.Tests;

public class ImdctTests
{
    [Theory]
    [InlineData(16)]
    [InlineData(64)]
    [InlineData(256)]
    [InlineData(2048)]
    public void MatchesTheDefinition(int n)
    {
        var input = new float[n / 2];
        var seed = 12345u;
        for (var i = 0; i < input.Length; i++)
        {
            seed = seed * 1103515245 + 12345;
            input[i] = (seed >> 16 & 0x7FFF) / 16384f - 1f;
        }

        var fast = new float[n];
        new Imdct(n).Inverse(input, fast);

        for (var i = 0; i < n; i++)
        {
            var direct = 0.0;
            for (var k = 0; k < n / 2; k++)
            {
                direct += input[k] * Math.Cos(Math.PI / (2 * n) * (2 * i + 1 + n / 2) * (2 * k + 1));
            }

            Assert.True(Math.Abs(direct - fast[i]) < 1e-3 * Math.Sqrt(n), $"n={n} i={i}: {direct} vs {fast[i]}");
        }
    }
}
