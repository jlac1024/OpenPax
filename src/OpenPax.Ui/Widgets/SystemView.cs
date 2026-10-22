using OpenPax.Render;
using OpenPax.Sim.Galaxy;
using OpenPax.Sim.Players;

namespace OpenPax.Ui.Widgets;

/// <summary>
/// The system view of a map pane: the star in the middle,
/// its wormholes, planets with their moons and orbitals, fleets, names and
/// the selection marker. System coordinates run 0 to 441 with the star at
/// 220, 220, scaled by 441 on both axes.
/// </summary>
internal sealed class SystemView(MapPane pane, UiAssets assets)
{
    private const int StarFrameTime = 100;
    private const int PlanetFrameTime = 125;
    private const int WormholeFrameTime = 66;
    private const int MarkerFrameTime = 100;
    private const int MoonStepTime = 40;
    private const int Center = 220;
    private const int MineFieldKind = 10;

    // The idle loop once a wormhole is back at frame 0.
    private static readonly int[] _idleLoop = [0, 1, 2, 3, 4, 5, 6, 7, 37, 38, 39, 40, 41, 42, 43, 44];

    public const int FlashDraws = 15;

    /// <summary>System coordinates to the pane: round half up of width * p / 441.</summary>
    public (int X, int Y) ToPane(int px, int py)
    {
        return (pane.ContentLeft + (int)((pane.ContentWidth * px * (1.0 / 441)) + 0.5), pane.ContentTop + (int)((pane.ContentHeight * py * (1.0 / 441)) + 0.5));
    }

    /// <summary>A point on the pane to system coordinates.</summary>
    public (int X, int Y) FromPane(int x, int y)
    {
        return ((x - pane.ContentLeft) * 441 / pane.ContentWidth, (y - pane.ContentTop) * 441 / pane.ContentHeight);
    }

    /// <summary>
    /// What is under a system point: within 20 units on both
    /// axes, fleets not in battle, then planets, then wormholes, then the
    /// star; the nearest by whole distance wins, an earlier kind on ties.
    /// </summary>
    /// <summary>The markers of the battles in the current system.</summary>
    private static IEnumerable<(int X, int Y)> Markers(GameSession session) =>
        session.Game.Battles.Where(b => b.Star == session.CurrentStar && b.Marker is not null).Select(b => b.Marker!.Value); // filtered to battles with a marker

    /// <summary>A battle whose marker is within 20 units on both axes of a system point, the first found.</summary>
    public OpenPax.Sim.Combat.Battle? MarkerAt(GameSession session, int px, int py)
    {
        return session.Game.Battles.FirstOrDefault(b => b.Star == session.CurrentStar && b.Marker is { } m && Math.Abs(px - m.X) <= 20 && Math.Abs(py - m.Y) <= 20);
    }

    public object? HitTest(Star star, int px, int py)
    {
        object? best = null;
        var bestDistance = 0xFFFFF;
        void Consider(object candidate, int x, int y)
        {
            if (Math.Abs(px - x) > 20 || Math.Abs(py - y) > 20)
            {
                return;
            }

            var d = (int)Math.Sqrt(((px - x) * (px - x)) + ((py - y) * (py - y)));
            if (d < bestDistance)
            {
                (best, bestDistance) = (candidate, d);
            }
        }

        foreach (var fleet in star.Fleets.Where(f => !f.InCombat))
        {
            Consider(fleet, fleet.X, fleet.Y);
        }

        foreach (var planet in star.Planets)
        {
            Consider(planet, planet.X, planet.Y);
        }

        // Wormholes and the star are taken first come, not nearest, within their kind.
        if (star.Wormholes.FirstOrDefault(w => Math.Abs(px - w.X) <= 20 && Math.Abs(py - w.Y) <= 20) is { } hole)
        {
            Consider(hole, hole.X, hole.Y);
        }

        Consider(star, Center, Center);
        return best;
    }

    public void Draw(Framebuffer target, GameSession session, long now, long since)
    {
        var star = session.Current;
        var sight = star.Sight[session.Local.Index];
        var font = assets.Fonts.ForIndex(3);
        var nameColor = sight == Star.Unseen ? (byte)0x19 : (byte)0x16;
        if (sight == Star.Unseen)
        {
            font.Draw(target, UiText.Unexplored, pane.Left + pane.Width - 2, pane.Top + 31, 0, 2, 0x19, 0);
        }

        var (cx, cy) = ToPane(Center, Center);
        if (pane.BigLook)
        {
            var frame = (int)(now / StarFrameTime % 15);
            MapPane.DrawCentered(target, assets.Pictures(ImageSet.Universe).Get(5000 + (50 * star.Type) + frame), cx, cy);
        }

        // The small look's pictures (Universe 6000 and 20054 on) are not in
        // the game's data; nothing is drawn for them.
        if (sight != Star.Unseen && pane.BigLook)
        {
            foreach (var hole in star.Wormholes)
            {
                DrawWormhole(target, session, hole, now);
            }

            foreach (var planet in star.Planets)
            {
                DrawPlanet(target, session, star, planet, now, since);
            }

            if (sight == Star.InView)
            {
                foreach (var fleet in star.Fleets.Where(f => !f.InCombat))
                {
                    DrawFleet(target, fleet);
                }
            }

            if (sight == Star.InView)
            {
                DrawFights(target, session.Game.Fights(star), now);
            }

            // each battle's marker, 15 frames at 100 ms.
            foreach (var marker in Markers(session))
            {
                var (mx, my) = ToPane(marker.X, marker.Y);
                MapPane.DrawCentered(target, assets.Pictures(ImageSet.Universe).Get(1000 + (int)(now / MarkerFrameTime % 15)), mx, my);
            }
        }

        if (sight == Star.Remembered)
        {
            // a hatch over the whole pane, every second row.
            for (var y = pane.Top; y < pane.Top + pane.Height; y += 2)
            {
                target.FillRect(pane.Left, y, pane.Width, 1, 0xB5);
            }
        }

        font.Draw(target, star.Name, pane.Left + pane.Width - 2, pane.Top + 15, 0, 2, nameColor, 0);
    }

    /// <summary>The selection marker, when the selection is in this system.</summary>
    public void DrawMarker(Framebuffer target, GameSession session, int frame)
    {
        var at = session.Selection switch
        {
            Planet p when session.Current.Planets.Contains(p) => (p.X, p.Y),
            Fleet f when f.Star == session.CurrentStar => (f.X, f.Y),
            Wormhole w when w.Star == session.CurrentStar => (w.X, w.Y),
            Star s when s == session.Current => (Center, Center),
            _ => ((int, int)?)null,
        };

        // With nothing here the original draws the marker at the screen's
        // corner, under the top bar; the port leaves it out.
        if (at is { } point)
        {
            var (x, y) = ToPane(point.Item1, point.Item2);
            var id = (pane.BigLook ? 1000 : 1050) + (pane.AltColors ? 50 : 0) + frame;
            MapPane.DrawCentered(target, assets.Pictures(ImageSet.Infra).Get(id), x, y);
        }
    }

    /// <summary>The flash where the last order leads, for 15 redraws.</summary>
    public void DrawOrderFlash(Framebuffer target, GameSession session, Fleet fleet, int count)
    {
        if (fleet.Orders.Count == 0 || count >= FlashDraws)
        {
            return;
        }

        var last = fleet.Orders[^1];
        var star = last.Target switch
        {
            Wormhole w when fleet.Orders.Count > 1 && w.Partner is { } partner => partner.Star,
            Wormhole w => w.Star,
            Planet p => session.Game.Universe.Stars.First(s => s.Planets.Contains(p)).Index,
            Fleet f => f.Star,
            _ => fleet.Star,
        };
        var (px, py) = last.Target is Wormhole { Partner: { } far } && fleet.Orders.Count > 1 ? (far.X, far.Y) : last.Position;
        if (star == session.CurrentStar)
        {
            var (x, y) = ToPane(px, py);
            MapPane.DrawCentered(target, assets.Pictures(ImageSet.Universe).Get(100 + count), x, y);
        }
    }

    private void DrawWormhole(Framebuffer target, GameSession session, Wormhole hole, long now)
    {
        var (x, y) = ToPane(hole.X, hole.Y);
        var frame = WormholeFrame(hole.AnimationFrame, now / WormholeFrameTime);
        MapPane.DrawCentered(target, assets.Pictures(ImageSet.Universe).Get(8000 + frame), x, y);
        if (hole.Partner is { } partner)
        {
            var far = session.Game.Universe.Stars[partner.Star];
            var color = far.Sight[session.Local.Index] == Star.Unseen ? (byte)0x19 : (byte)0x16;
            assets.Fonts.ForIndex(1).Draw(target, far.Name, x, y + 16, 0, 1, color, 0);
        }
    }

    /// <summary>
    /// The idle wormhole loop: frames 0 to 7 then 37 to 44, one step per 66
    /// ms; a start inside 9 to 36 plays on to 44 first. The opening run after
    /// a jump (all 45 frames) is not ported.
    /// </summary>
    private static int WormholeFrame(int start, long steps)
    {
        var frame = start;
        for (var walked = 0L; walked < steps; walked++)
        {
            if (frame == 0)
            {
                return _idleLoop[(steps - walked) % _idleLoop.Length];
            }

            frame = frame == 8 ? 37 : frame >= 44 ? 0 : frame + 1;
        }

        return frame;
    }

    private void DrawPlanet(Framebuffer target, GameSession session, Star star, Planet planet, long now, long since)
    {
        var (x, y) = ToPane(planet.X, planet.Y);
        var view = (int)(Wrap(planet.Orbit.Angle + 5.0) / 10.0);
        var spin = (int)((planet.AnimationFrame + (now / PlanetFrameTime)) % 20);
        MapPane.DrawCentered(target, assets.Pictures(ImageSet.Planets).Get(PlanetBase(planet) + (20 * view) + spin), x, y);

        var seen = star.Sight[session.Local.Index] == Star.InView;
        if (seen && planet.Colony is { } colony)
        {
            if (assets.Pictures(ImageSet.Empire).Get(colony.Owner.Flag + 6000) is { } flag)
            {
                target.Draw(flag, x - 15, y - 15);
            }

            if (colony.FirstOfKind(TechInfo.ShipyardKind) is not null && assets.Pictures(ImageSet.Main).Get(1100) is { } yard)
            {
                target.Draw(yard, x - 15, y + 15 - yard.Height);
            }
        }

        DrawSatellites(target, session, planet, seen, x, y, since);

        var color = seen && planet.Colony is { } c ? MapPane.RelationColor(session.Local, c.Owner) : (byte)0xFF;
        var label = assets.Strings.Get(new Data.Views.TextRef(111, planet.Slot));
        assets.Fonts.ForIndex(0).Draw(target, label, x, y + (planet.Size / 2), 0, 1, color, 0);
    }

    /// <summary>
    /// Moons, then a dot per orbital unit and one per mine.
    /// Their angles move on every redraw of the big pane, every 40 ms, so the
    /// motion belongs to the screen, not the game.
    /// </summary>
    private void DrawSatellites(Framebuffer target, GameSession session, Planet planet, bool seen, int px, int py, long since)
    {
        var orbitals = 0;
        var mines = 0;
        if (seen && planet.Colony is { } colony)
        {
            orbitals = colony.Structures.Where(s => s.Tech.Kind != MineFieldKind).Sum(s => s.Count);
            mines = colony.Structures.Where(s => s.Tech.Kind == MineFieldKind).Sum(s => s.Count);
        }

        var steps = since / MoonStepTime;
        var universe = assets.Pictures(ImageSet.Universe);
        for (var i = 0; i < planet.Satellites.Count && i < planet.Moons + orbitals + mines; i++)
        {
            var moon = planet.Satellites[i];
            var angle = (int)Wrap(moon.Angle + (moon.Speed * steps));
            var x = (int)((session.Game.Tables.Cos[angle] * moon.Radius) + px);
            var y = (int)((session.Game.Tables.Sin[angle] * moon.Radius) + py);
            var picture = i < planet.Moons ? 28 : i < planet.Moons + orbitals ? 29 : 30;
            MapPane.DrawCentered(target, universe.Get(picture), x, y);
        }
    }

    /// <summary>
    /// The first of a planet's 720 pictures: 36 views of 20
    /// spin frames. Rocky classes 0 to 4 have their own runs; gas giants go by
    /// type, with a cold run below -25 degrees.
    /// </summary>
    internal static int PlanetBase(Planet planet)
    {
        return planet.Class < 5
            ? 300001 + (10000 * planet.Class)
            : 1 + (100000 * planet.Type) + (planet.Temperature <= -26 ? 1000 : 0);
    }

    private void DrawFleet(Framebuffer target, Fleet fleet)
    {
        var (x, y) = ToPane(fleet.X, fleet.Y);
        var index = 0;
        if (fleet.Orders.Count > 0)
        {
            var (tx, ty) = fleet.Orders[0].Position;
            var heading = Sim.Numerics.X87.DirectionDegrees(tx - fleet.X, ty - fleet.Y);
            index = (int)((heading + 6.625) * (32.0 / 360.0));
            if (index < 0)
            {
                index += 32;
            }
            else if (index > 32)
            {
                index -= 32;
            }
        }

        MapPane.DrawCentered(target, assets.Pictures(ImageSet.Infra).Get(((fleet.Owner.FleetColor + 1) * 50) + index), x, y);
        if (assets.Pictures(ImageSet.Empire).Get(fleet.Owner.Flag + 6000) is { } flag)
        {
            target.Draw(flag, x - 15, y - 15);
        }

        if (fleet.Intercepting && assets.Pictures(ImageSet.Main).Get(1101) is { } intercept)
        {
            target.Draw(intercept, x, y - 15);
        }
    }

    /// <summary>
    /// The port's own, for fights a mod runs without the battle screen
    /// (docs/modding.md): the original's battle marker on each target, and a
    /// health bar by each side: under a fleet, above a planet (its defenses,
    /// then its people).
    /// </summary>
    private void DrawFights(Framebuffer target, IReadOnlyList<Sim.ModFight> fights, long now)
    {
        if (fights.Count == 0)
        {
            return;
        }

        var (good, hurt, gone, people) = (target.Nearest(40, 200, 40), target.Nearest(230, 40, 30), target.Nearest(20, 20, 20), target.Nearest(220, 200, 60));
        var shown = new HashSet<ISpaceObject>();
        foreach (var fight in fights)
        {
            var (tx, ty) = ToPane(fight.Target.X, fight.Target.Y);
            if (shown.Add(fight.Target))
            {
                MapPane.DrawCentered(target, assets.Pictures(ImageSet.Universe).Get(1000 + (int)(now / MarkerFrameTime % 15)), tx, ty);
                if (fight.Target is Fleet defender)
                {
                    Bar(defender.Ships.Sum(s => s.Structure), defender.Ships.Sum(s => s.MaxStructure), tx, ty + 14, good);
                }
                else if (fight.Target is Planet { Colony: { } colony })
                {
                    var defenses = colony.Structures.Where(s => s.Tech.Kind is >= 7 and <= 10).ToList();
                    // Above the planet, clear of its name below.
                    if (defenses.Count > 0)
                    {
                        Bar(defenses.Sum(s => s.HitPoints), defenses.Sum(s => s.MaxHitPoints), tx, ty - 26, good);
                    }

                    Bar(colony.CurrentPopulation, Math.Max(colony.MaxPopulation, colony.CurrentPopulation), tx, ty - 22, people);
                }
            }

            if (shown.Add(fight.Fleet))
            {
                var (ax, ay) = ToPane(fight.Fleet.X, fight.Fleet.Y);
                Bar(fight.Fleet.Ships.Sum(s => s.Structure), fight.Fleet.Ships.Sum(s => s.MaxStructure), ax, ay + 14, good);
            }
        }

        void Bar(double left, double full, int x, int y, byte color)
        {
            const int Width = 24;
            var filled = full <= 0 ? 0 : (int)Math.Ceiling(Math.Clamp(left / full, 0, 1) * Width);
            target.FillRect(x - (Width / 2) - 1, y - 1, Width + 2, 5, gone);
            target.FillRect(x - (Width / 2), y, filled, 3, filled * 3 < Width ? hurt : color);
        }
    }

    private static double Wrap(double degrees)
    {
        var a = degrees % 360.0;
        return a < 0 ? a + 360.0 : a;
    }
}
