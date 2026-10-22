using OpenPax.Sim.Numerics;

namespace OpenPax.Sim.Tests;

public class X87Tests
{
    [Fact]
    public void ToIntFloorsFractionsAndShiftsNegativeWholeNumbers()
    {
        Assert.Equal(3, X87.ToInt(3.7));
        Assert.Equal(-4, X87.ToInt(-3.2));
        // The original's quirk: a negative whole number lands one lower.
        Assert.Equal(-4, X87.ToInt(-3.0));
        Assert.Equal(0, X87.ToInt(0.0));
    }

    [Fact]
    public void DirectionUsesExactAxisValues()
    {
        Assert.Equal(0.0, X87.DirectionDegrees(5, 0));
        Assert.Equal(0.0, X87.DirectionDegrees(0, 0));
        Assert.Equal(180.0, X87.DirectionDegrees(-5, 0));
        Assert.Equal(90.0, X87.DirectionDegrees(0, 5));
        Assert.Equal(270.0, X87.DirectionDegrees(0, -5));
    }

    [Fact]
    public void MatchesTheRealX87Instructions()
    {
        var path = ReferenceFile();
        Assert.SkipWhen(path is null, "build/native/x87_reference.bin not found; build and run tools/x87_reference.c");

        // Checked by the skip above.
        var data = File.ReadAllBytes(path!);
        int records = 0, bitDiffs = 0, positionDiffs = 0;
        for (var at = 0; at + 25 <= data.Length; at += 25)
        {
            var op = data[at];
            var a = BitConverter.ToDouble(data, at + 1);
            var b = BitConverter.ToDouble(data, at + 9);
            var expected = BitConverter.ToDouble(data, at + 17);
            var actual = op switch
            {
                0 or 2 => X87.CosTimes(a, b),
                1 or 3 => X87.SinTimes(a, b),
                _ => X87.DirectionDegrees((int)b, (int)a),
            };

            records++;
            if (BitConverter.DoubleToInt64Bits(actual) != BitConverter.DoubleToInt64Bits(expected))
            {
                bitDiffs++;
            }

            // What the game keeps: integer positions around the system view
            // center, or for directions the wormhole offset they lead to.
            var (exp, act) = op switch
            {
                0 or 1 => (X87.ToInt(220 + expected), X87.ToInt(220 + actual)),
                2 or 3 => (X87.ToInt(expected), X87.ToInt(actual)),
                _ => (X87.ToInt(X87.CosTimes(X87.DegreesToRadians(expected), 175.0)), X87.ToInt(X87.CosTimes(X87.DegreesToRadians(actual), 175.0))),
            };

            if (exp != act)
            {
                positionDiffs++;
            }
        }

        Assert.True(positionDiffs == 0 && bitDiffs * 1000L < records, $"{records} records: {bitDiffs} differ in some bit, {positionDiffs} give a different position");
    }

    private static string? ReferenceFile()
    {
        for (var dir = new DirectoryInfo(AppContext.BaseDirectory); dir is not null; dir = dir.Parent)
        {
            var file = Path.Combine(dir.FullName, "build", "native", "x87_reference.bin");
            if (File.Exists(file))
            {
                return file;
            }
        }

        return null;
    }
}
