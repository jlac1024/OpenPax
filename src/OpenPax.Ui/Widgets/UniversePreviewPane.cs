using OpenPax.Data.Views;
using OpenPax.Render;
using OpenPax.Sim;
using OpenPax.Sim.Galaxy;

namespace OpenPax.Ui.Widgets;

/// <summary>
/// Class 10001 in Game Setup: the galaxy a seed makes, drawn small. It
/// places all 100 stars as the universe does and shows the first ones.
/// </summary>
public sealed class UniversePreviewPane(ViewNode node, int left, int top) : Pane(node, left, top)
{
    private int[] _x = [];
    private int[] _y = [];
    private int[] _types = [];

    public uint Seed { get; private set; }

    /// <summary>Stars shown, at most 100.</summary>
    public int Count { get; set; } = 20;

    /// <summary>Makes the preview for a seed.</summary>
    public void Generate(uint seed)
    {
        Seed = seed;
        (_x, _y, _types) = UniverseGenerator.PlaceStars(seed, UniverseGenerator.MaxStars, new GameRandom(seed));
    }

    /// <summary>
    /// NEW UNIVERSE: the next seed is R(10000) from the stream
    /// the preview just used, so it follows from the old seed alone.
    /// </summary>
    public void NewUniverse()
    {
        var random = new GameRandom(Seed);
        UniverseGenerator.PlaceStars(Seed, UniverseGenerator.MaxStars, random);
        Generate((uint)random.Next(10000));
    }

    protected override void Draw(Framebuffer target)
    {
        var universe = Assets.Pictures(ImageSet.Universe);
        if (universe.Get(1) is { } backdrop)
        {
            target.Draw(backdrop, Left, Top);
        }

        for (var i = 0; i < Math.Min(Count, _x.Length); i++)
        {
            if (universe.Get(2000 + _types[i]) is { } star)
            {
                // Centered on the point, scaled from the 450 square to the panel.
                var x = Left + Width * _x[i] / 450;
                var y = Top + Height * _y[i] / 450;
                target.Draw(star, x - star.Width / 2, y - star.Height / 2);
            }
        }
    }
}
