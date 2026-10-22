using System.Globalization;
using OpenPax.Data.Views;
using OpenPax.Render;
using OpenPax.Sim;
using OpenPax.Sim.Galaxy;
using OpenPax.Sim.Numerics;
using OpenPax.Sim.Players;

namespace OpenPax.Ui.Widgets;

/// <summary>
/// Class 14900, a planet's figures on the Planetary Surface: habitability,
/// wealth, and for the player's own colony its growth, population with a
/// bar, income less upkeep, espionage, research and construction; "--" for
/// anything else.
/// </summary>
public sealed class ColonyStatsPane(ViewNode node, int left, int top) : Pane(node, left, top)
{
    private static readonly CultureInfo _invariant = CultureInfo.InvariantCulture;

    public Planet? Planet { get; set; }

    public GameSession? Session { get; set; }

    private string S(int list, int entry) => Assets.Strings.Get(new TextRef(list, entry));

    private static string Commas(double value) => X87.ToInt(value).ToString("#,0", _invariant);

    protected override void Draw(Framebuffer target)
    {
        if (Planet is not { } planet || Session is not { } session)
        {
            return;
        }

        var (l, t, r) = (Left, Top, Left + Width);
        var local = session.Local;
        if (planet.Class != 5)
        {
            MapPane.DrawCentered(target, Assets.Pictures(ImageSet.Main).Get(14014 + Colony.Habitability(local.Species, planet)), r - 18, t + 13);
        }

        var star = session.Game.Universe.Stars.First(s => s.Planets.Contains(planet));
        var colony = planet.Colony;
        byte color = 0xFF;
        if (colony is not null && star.Sight[local.Index] == Star.InView)
        {
            var level = colony.Owner == local ? 6 : local.Treaties[colony.Owner.Index].Level();
            color = level switch
            {
                Treaty.None => 0xFF,
                < 1 => 0x12,
                <= 3 => 0x10,
                _ => 0x16,
            };
        }

        var font = Assets.Fonts.ForIndex(1);
        int[] labels = [1, 2, 4, 6, 5, 7];
        int[] rows = [16, 30, 56, 70, 84, 98];
        for (var i = 0; i < labels.Length; i++)
        {
            font.Draw(target, S(14000, labels[i]), l + 8, t + rows[i], 0, 0, color, 0);
        }

        void Value(string text, int y) => font.Draw(target, text, l + 167, t + y, 0, 2, color, 0);
        Value(S(14001, X87.ToInt(planet.Score - 1.0)), 16);

        if (colony is null || colony.Owner != local)
        {
            // Quirk kept: the growth line stays blank.
            Value(S(109, 3) + X87.ToInt(Colony.MaximumPopulation(local, planet, colony)).ToString(_invariant), 42);
            foreach (var y in new[] { 56, 70, 84, 98 })
            {
                Value(S(109, 4), y);
            }

            return;
        }

        // Quirk kept: a shrinking colony reads "+-0.3".
        Value("+" + (colony.Growth * 10.0).ToString("0.0", _invariant), 30);
        Value(Commas(colony.CurrentPopulation) + S(108, 4) + colony.MaxPopulation.ToString(_invariant), 42);
        var (bl, bt, br, bb) = (l + 8, t + 36, l + 127, t + 41);
        target.DrawLine(bl, bt, br, bt, 0x16);
        target.DrawLine(bl, bt, bl, bb, 0x16);
        target.DrawLine(bl, bb - 1, br, bb - 1, 0x16);
        target.DrawLine(br - 1, bt, br - 1, bb, 0x16);
        var share = colony.MaxPopulation > 0 ? Math.Clamp(colony.CurrentPopulation / colony.MaxPopulation, 0.0, 1.0) : 0.0;
        target.FillRect(bl + 1, bt + 1, X87.ToInt((br - bl - 2) * share), bb - bt - 2, 0x12);

        Value(S(104, 0) + X87.ToInt((colony.Resources[Colony.Credits] * 10.0) - Game.ColonyUpkeep(colony)).ToString(_invariant), 56);
        Value(Commas(colony.Resources[Colony.Espionage] * 10.0), 70);
        Value(Commas(colony.Resources[Colony.ResearchResource] * 10.0), 84);
        Value(Commas(colony.Resources[Colony.Construction] * 10.0), 98);
    }
}
