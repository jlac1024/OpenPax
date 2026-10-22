using OpenPax.Data.Art;

namespace OpenPax.Render.TrueColor;

/// <summary>
/// A 32-bit color as the canvas keeps it: red, green, blue and alpha bytes
/// in that order in memory (0xAABBGGRR read as a little-endian number),
/// which is what the SDL texture takes. Alpha is straight, not premultiplied.
/// </summary>
public readonly record struct Color(uint Packed)
{
    public static Color Transparent => new(0);

    public static Color Black => Rgb(0, 0, 0);

    public static Color White => Rgb(255, 255, 255);

    public byte R => (byte)Packed;

    public byte G => (byte)(Packed >> 8);

    public byte B => (byte)(Packed >> 16);

    public byte A => (byte)(Packed >> 24);

    public static Color Rgb(byte r, byte g, byte b, byte a = 255) => new(r | ((uint)g << 8) | ((uint)b << 16) | ((uint)a << 24));

    public static Color Of(Rgb c, byte a = 255) => Rgb(c.R, c.G, c.B, a);

    public Color WithAlpha(byte a) => new((Packed & 0x00FFFFFF) | ((uint)a << 24));

    /// <summary>"#RRGGBB" or "#AARRGGBB", as forms write colors.</summary>
    public static bool TryParse(string text, out Color color)
    {
        color = Transparent;
        if (text.Length is not (7 or 9) || text[0] != '#' || !uint.TryParse(text.AsSpan(1), System.Globalization.NumberStyles.HexNumber, System.Globalization.CultureInfo.InvariantCulture, out var v))
        {
            return false;
        }

        var a = text.Length == 9 ? (byte)(v >> 24) : (byte)255;
        color = Rgb((byte)(v >> 16), (byte)(v >> 8), (byte)v, a);
        return true;
    }

    /// <summary>This color laid over another by its alpha.</summary>
    public uint Over(uint below)
    {
        var a = A;
        if (a == 255)
        {
            return Packed;
        }

        if (a == 0)
        {
            return below;
        }

        var inv = 255 - a;
        var r = (R * a + (byte)below * inv) / 255;
        var g = (G * a + (byte)(below >> 8) * inv) / 255;
        var b = (B * a + (byte)(below >> 16) * inv) / 255;
        var outA = a + ((byte)(below >> 24) * inv / 255);
        return (uint)r | ((uint)g << 8) | ((uint)b << 16) | ((uint)outA << 24);
    }
}
