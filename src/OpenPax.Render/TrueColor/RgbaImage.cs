using OpenPax.Data.Art;

namespace OpenPax.Render.TrueColor;

/// <summary>A true-color picture: the game's art through its palette, or anything a mod brings.</summary>
public sealed class RgbaImage(int width, int height, uint[] pixels)
{
    public int Width { get; } = width;

    public int Height { get; } = height;

    /// <summary>Packed as <see cref="Color"/>, row by row.</summary>
    public uint[] Pixels { get; } = pixels;

    /// <summary>A palette picture in color; its never-drawn index becomes clear.</summary>
    public static RgbaImage From(Sprite sprite, Palette palette)
    {
        var colors = palette.Colors;
        var pixels = new uint[sprite.Width * sprite.Height];
        for (var i = 0; i < pixels.Length; i++)
        {
            var index = sprite.Pixels[i];
            pixels[i] = index == Sprite.TransparentIndex ? 0 : Color.Of(colors[index]).Packed;
        }

        return new RgbaImage(sprite.Width, sprite.Height, pixels);
    }

    public Color At(int x, int y) => new(Pixels[y * Width + x]);

    /// <summary>A part of the picture as a picture of its own (a skin cut from a background).</summary>
    public RgbaImage Crop(int x, int y, int width, int height)
    {
        var pixels = new uint[width * height];
        for (var row = 0; row < height; row++)
        {
            Array.Copy(Pixels, (y + row) * Width + x, pixels, row * width, width);
        }

        return new RgbaImage(width, height, pixels);
    }

    /// <summary>A converted PNG in its own palette and transparency.</summary>
    public static RgbaImage From(Data.Png.IndexedImage png)
    {
        var pixels = new uint[png.Width * png.Height];
        for (var i = 0; i < pixels.Length; i++)
        {
            var index = png.Pixels[i];
            var alpha = index < png.Alpha.Count ? png.Alpha[index] : (byte)255;
            pixels[i] = Color.Of(png.Palette[index], alpha).Packed;
        }

        return new RgbaImage(png.Width, png.Height, pixels);
    }
}
