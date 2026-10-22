using System.Globalization;
using OpenPax.Data.Views;
using OpenPax.Render;
using OpenPax.Sim.Galaxy;
using OpenPax.Sim.Players;

namespace OpenPax.Ui.Widgets;

/// <summary>
/// Class 19650 on the Sector Display: details of the selection. A star
/// lists its planets; a planet shows its picture, defenses and figures; a
/// fleet its figures. Text positions are baselines from the panel's corner.
/// </summary>
public sealed class InfoPanelPane(ViewNode node, int left, int top) : Pane(node, left, top)
{
    private const int GasGiant = 5;

    // Each planet of the star's list takes this many rows.
    private const int RowHeight = 66;

    private static readonly CultureInfo _invariant = CultureInfo.InvariantCulture;

    // Defense pictures: kind, first picture for a count, picture, count and health bar positions.
    private static readonly (int Kind, int Base, int PicX, int PicY, int CountX, int CountY, int BarX, int BarY)[] _defenses =
    [
        (9, 119, 9, 270, 9, 234, 14, 270),
        (7, 169, 21, 313, 19, 293, 24, 313),
        (8, 129, 57, 302, 55, 272, 65, 297),
        (5, -1, 108, 377, 108, 357, 113, 377),
        (6, -1, 105, 330, 100, 310, 105, 335),
    ];

    public GameSession? Session { get; set; }

    private int L => Left;

    private int T => Top;

    private int B => Top + Height;

    protected override void Draw(Framebuffer target)
    {
        if (Session is not { } session)
        {
            return;
        }

        switch (session.Selection)
        {
            case Planet planet:
                DrawPlanetPictures(target, session, planet);
                DrawPlanetText(target, session, planet);
                break;
            case Fleet fleet:
                DrawFleet(target, session, fleet);
                break;
            case Star star:
                DrawStar(target, session, star);
                break;
        }

        if (Assets.Pictures(ImageSet.Main).Get(13012) is { } corner)
        {
            target.Draw(corner, L, B - corner.Height);
        }
    }

    /// <summary>
    /// The port's own: a click on a planet's row of the star's list selects
    /// the planet, as a click on it in the system view does (gas giants, which
    /// the view cannot pick either, excepted).
    /// </summary>
    protected internal override void MouseDown(int x, int y, int clicks)
    {
        if (Session is not { Selection: Star star } session || star.Sight[session.Local.Index] == Star.Unseen)
        {
            return;
        }

        var k = (y - T) / RowHeight;
        if (y >= T && k < star.Planets.Count && star.Planets[k] is { } planet && planet.Class != GasGiant)
        {
            session.Select(planet);
        }
    }

    private string Str(int list, int index)
    {
        return Assets.Strings.Get(new TextRef(list, index));
    }

    private static string Number(double value)
    {
        return ((int)value).ToString(_invariant);
    }

    /// <summary>One decimal, as printf's "%.1f".</summary>
    private static string OneDecimal(double value) => value.ToString("F1", _invariant);

    private static bool OwnSeen(GameSession session, Planet planet, Star star)
    {
        return planet.Colony is { } c && c.Owner == session.Local && star.Sight[session.Local.Index] == Star.InView;
    }

    private void DrawStar(Framebuffer target, GameSession session, Star star)
    {
        var local = session.Local;
        if (star.Sight[local.Index] == Star.Unseen)
        {
            Assets.Fonts.ForIndex(1).Draw(target, Str(83, 7), (L + L + Width) / 2, T + 30, 0, 1, 0xFF, 0);
            return;
        }

        var font = Assets.Fonts.ForIndex(0);
        for (var k = 0; k < star.Planets.Count; k++)
        {
            var planet = star.Planets[k];
            var d = RowHeight * k;
            MapPane.DrawCentered(target, Assets.Pictures(ImageSet.Planets).Get(PlanetPicture(planet) + 20), L + 22, T + 24 + d);

            var color = (byte)0xFF;
            if (planet.Colony is { } colony && star.Sight[local.Index] == Star.InView)
            {
                color = MapPane.RelationColor(local, colony.Owner);
                MapPane.DrawCentered(target, Assets.Pictures(ImageSet.Empire).Get(colony.Owner.Flag + 1000), L + 46, T + 18 + d);
            }

            font.Draw(target, planet.Name, L + 36, T + 38 + d, 0, 0, color, 0);

            // The port's own: a mod's rating of the colony (docs/modding.md), under its name.
            if (planet.Colony is { } rated && star.Sight[local.Index] == Star.InView && session.Game.RateColony?.Invoke(session.Game, rated) is { } rating)
            {
                font.Draw(target, string.Format(_invariant, UiText.ColonyRating, rating.Attack, rating.Defense), L + 36, T + 52 + d, 0, 0, color, 0);
            }

            var main = Assets.Pictures(ImageSet.Main);
            if (planet.Class == GasGiant)
            {
                MapPane.DrawCentered(target, main.Get(17017), L + 138, T + 34 + d);
                continue;
            }

            var value = OwnSeen(session, planet, star) && planet.Colony is { } own
                ? Number(own.CurrentPopulation) + Str(108, 4) + own.MaxPopulation.ToString(_invariant)
                : Str(109, 3) + Colony.MaximumPopulation(local, planet, planet.Colony).ToString(_invariant);
            font.Draw(target, value, L + 128, T + 38 + d, 0, 2, color, 0);
            MapPane.DrawCentered(target, main.Get(17001 + Colony.Habitability(local.Species, planet)), L + 138, T + 34 + d);
            font.Draw(target, Str(14001, (int)(planet.Score - 1.0)), L + 146, T + 18 + d, 0, 2, color, 0);
        }
    }

    private void DrawPlanetPictures(Framebuffer target, GameSession session, Planet planet)
    {
        var universe = Assets.Pictures(ImageSet.Universe);
        DrawBottomLeft(target, universe.Get(22120 + planet.Class), L, B);

        var star = session.Game.Universe.Stars.First(s => s.Planets.Contains(planet));
        if (planet.Colony is not { } colony || star.Sight[session.Local.Index] != Star.InView)
        {
            return;
        }

        // City lights grow with the population improvements built.
        var built = colony.Improvements.Count(i => i is { Built: > 0.0 } && i.Tech.Resource == Colony.Population);
        if (built > 0)
        {
            DrawBottomLeft(target, universe.Get(((built - 1) / 5) + (planet.Class == 4 ? 22000 : 22010)), L, B);
        }

        var font = Assets.Fonts.ForIndex(1);
        var color = MapPane.RelationColor(session.Local, colony.Owner);
        foreach (var d in _defenses)
        {
            if (colony.FirstOfKind(d.Kind) is not { } stack)
            {
                continue;
            }

            var picture = d.Kind switch
            {
                5 => 16000,
                6 => 14000,
                _ => (stack.Count + d.Base) * 100,
            };
            DrawBottomLeft(target, universe.Get(picture), L + d.PicX, T + d.PicY);
            font.Draw(target, stack.Count.ToString(_invariant), L + d.CountX, T + d.CountY, 0, 0, color, 0);
            DrawHealth(target, L + d.BarX, T + d.BarY, stack.Percent * 0.01, color);
        }

        if (colony.FirstOfKind(10) is { } mines)
        {
            for (var j = 0; j < 8; j++)
            {
                MapPane.DrawCentered(target, universe.Get((mines.Count + 149) * 100), L + 9 + (15 * j), T + 175 + (12 * j));
            }

            font.Draw(target, mines.Count.ToString(_invariant), L + 65, T + 210, 0, 0, color, 0);
            DrawHealth(target, L + 75, T + 210, mines.Percent * 0.01, color);
        }
    }

    /// <summary>
    /// A 30 by 3 health bar: the empty bar, then the full one
    /// clipped to the share. The original draws the full one in a color mode
    /// of the relation color; the port draws its picture as is.
    /// </summary>
    private void DrawHealth(Framebuffer target, int x, int y, double share, byte color)
    {
        var combat = Assets.Pictures(ImageSet.Combat);
        target.SetClip(x, y, 30, 3);
        if (combat.Get(20002) is { } empty)
        {
            target.Draw(empty, x, y);
        }

        target.SetClip(x, y, (int)(share * 30), 3);
        if (combat.Get(20003) is { } full)
        {
            target.Draw(full, x, y);
        }

        target.SetClip(Left, Top, Width, Height);
    }

    private void DrawPlanetText(Framebuffer target, GameSession session, Planet planet)
    {
        var local = session.Local;
        var star = session.Game.Universe.Stars.First(s => s.Planets.Contains(planet));
        var seen = star.Sight[local.Index] == Star.InView;
        var color = seen && planet.Colony is { } c ? MapPane.RelationColor(local, c.Owner) : (byte)0xFF;

        Assets.Fonts.ForIndex(2).Draw(target, planet.Name, L + 22, T + 15, 1, 0, color, 0);

        // The port's own: a mod's planet type is named at the right of the title (docs/modding/content.md).
        if (planet.Kind > 0 && planet.Kind <= session.Game.Tables.PlanetKinds.Count)
        {
            Assets.Fonts.ForIndex(0).Draw(target, session.Game.Tables.PlanetKinds[planet.Kind - 1].Name, L + Width - 6, T + 18, 0, 2, color, 0);
        }

        if (planet.Class != GasGiant)
        {
            MapPane.DrawCentered(target, Assets.Pictures(ImageSet.Main).Get(17001 + Colony.Habitability(local.Species, planet)), L + 12, T + 10);
        }

        var font = Assets.Fonts.ForIndex(1);
        (int Index, int Y)[] labels = [(1, 35), (2, 51), (4, 79), (6, 96), (5, 113), (7, 130), (14, 147)];
        foreach (var (index, y) in labels)
        {
            font.Draw(target, Str(14000, index), L + 6, T + y, 0, 0, color, 0);
        }

        var right = L + 148;
        font.Draw(target, Str(14001, (int)(planet.Score - 1.0)), right, T + 35, 0, 2, color, 0);
        if (!(OwnSeen(session, planet, star) && planet.Colony is { } own))
        {
            var estimate = Str(109, 3) + Colony.MaximumPopulation(local, planet, planet.Colony).ToString(_invariant);
            font.Draw(target, estimate, right, T + 64, 0, 2, color, 0);
            foreach (var y in new[] { 79, 96, 113, 130, 147 })
            {
                font.Draw(target, Str(109, 4), right, T + y, 0, 2, color, 0);
            }

            return;
        }

        font.Draw(target, Number(own.CurrentPopulation) + Str(108, 4) + own.MaxPopulation.ToString(_invariant), right, T + 64, 0, 2, color, 0);

        // The last period's growth, ten times over (quirk kept: the sign is "+" even for a loss).
        font.Draw(target, UiText.Plus + OneDecimal(own.Growth * 10.0), right, T + 51, 0, 2, color, 0);

        // The population bar: a frame filled to the share of the maximum.
        target.FillRect(L + 6, T + 57, 108, 5, 0x16);
        var share = own.MaxPopulation > 0 ? Math.Clamp(own.CurrentPopulation / own.MaxPopulation, 0.0, 1.0) : 0.0;
        target.FillRect(L + 7, T + 58, (int)(106 * share), 3, 0x12);

        // Income less the colony's upkeep.
        var upkeep = Sim.Game.ColonyUpkeep(own);
        font.Draw(target, Str(104, 0) + Number((own.Resources[Colony.Credits] * 10) - upkeep), right, T + 79, 0, 2, color, 0);
        font.Draw(target, Number(own.Resources[Colony.Espionage] * 10), right, T + 96, 0, 2, color, 0);
        font.Draw(target, Number(own.Resources[Colony.ResearchResource] * 10), right, T + 113, 0, 2, color, 0);
        font.Draw(target, Number(own.Resources[Colony.Construction] * 10), right, T + 130, 0, 2, color, 0);
        font.Draw(target, session.Game.ColonyStrength(own).ToString(_invariant), right, T + 147, 0, 2, color, 0);
        if (WorkLine(session, own) is { } work)
        {
            font.Draw(target, work, right, T + 164, 0, 2, color, 0);
        }
    }

    /// <summary>
    /// What the colony is working on: the ship the shipyard is
    /// building, else a structure being built or repaired, else the next
    /// improvement; nothing when idle.
    /// </summary>
    internal string? WorkLine(GameSession session, Colony own)
    {
        var percent = Str(109, 2);
        if (own.FirstOfKind(TechInfo.ShipyardKind) is { Queue.Count: > 0 } yard)
        {
            var head = yard.Queue[0];
            var done = yard.Total > 0.0 ? (int)(yard.Progress / yard.Total * 100.0) : 0;
            return yard.Queue.Count.ToString(_invariant) + UiText.QueueSeparator + head.ClassName + UiText.ClassSeparator + head.Name + " " + done.ToString(_invariant) + percent;
        }

        if (Sim.Game.Damaged(own) is { } s)
        {
            int[] kinds = [TechInfo.ShipyardKind, TechInfo.TrainingBaseKind, TechInfo.FighterBaseKind, TechInfo.MissileBaseKind, TechInfo.BattleStationKind, TechInfo.MineFieldKind];
            var kind = Array.IndexOf(kinds, s.Tech.Kind);
            return Str(101, 6) + s.Count.ToString(_invariant) + " " + (kind >= 0 ? Str(101, kind) : "") + " " + ((int)s.Percent).ToString(_invariant) + percent;
        }

        return session.Game.NextImprovement(own) is { } next
            ? Str(100000 + next.Tech.Id, 0) + " " + ((int)next.Percent).ToString(_invariant) + percent
            : null;
    }

    private void DrawFleet(Framebuffer target, GameSession session, Fleet fleet)
    {
        var color = MapPane.RelationColor(session.Local, fleet.Owner);
        var font = Assets.Fonts.ForIndex(1);
        font.Draw(target, fleet.Name, L + 6, T + 15, 0, 0, color, 0);
        if (fleet.Owner == session.Local && Sim.Game.InTraining(fleet))
        {
            Assets.Fonts.ForIndex(0).Draw(target, Str(20002, 8), L + 60, T + 25, 0, 0, color, 0);
        }

        var colonyShips = Sim.Game.ColonyShips(fleet);
        if (colonyShips > 0)
        {
            MapPane.DrawCentered(target, Assets.Pictures(ImageSet.Main).Get(13010), L + 115, T + 12);
            font.Draw(target, colonyShips.ToString(_invariant) + "X", L + 140, T + 15, 0, 2, color, 0);
        }

        var condition = fleet.Ships.Count == 0 ? 0 : (int)fleet.Ships.Average(s => s.ConditionPercent());
        (string Label, string Value, int Y)[] rows =
        [
            (Str(20000, 6), OneDecimal(fleet.Speed), 32),
            (Str(20000, 7), condition.ToString(_invariant) + Str(109, 2), 47),
            (Str(20000, 8), fleet.Strength.ToString(_invariant), 62),
            (Str(21000, 9), fleet.JumpsLeft.ToString(_invariant) + "/" + fleet.JumpRange.ToString(_invariant), 77),
            (Str(21000, 12), fleet.Ships.Count.ToString(_invariant), 92),
        ];
        foreach (var (label, value, y) in rows)
        {
            font.Draw(target, label, L + 6, T + y, 0, 0, color, 0);
            font.Draw(target, value, L + 140, T + y, 0, 2, color, 0);
        }
    }

    /// <summary>The first of a planet's pictures, as the system view picks it.</summary>
    private static int PlanetPicture(Planet planet)
    {
        return planet.Class < GasGiant
            ? 300001 + (10000 * planet.Class)
            : 1 + (100000 * planet.Type) + (planet.Temperature <= -26 ? 1000 : 0);
    }

    private static void DrawBottomLeft(Framebuffer target, Sprite? sprite, int x, int bottom)
    {
        if (sprite is not null)
        {
            target.Draw(sprite, x, bottom - sprite.Height);
        }
    }
}
