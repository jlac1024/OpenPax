using System.Globalization;
using OpenPax.Data.Views;
using OpenPax.Render;
using OpenPax.Sim.Commands;
using OpenPax.Sim.Galaxy;
using OpenPax.Sim.Players;

namespace OpenPax.Ui.Widgets;

/// <summary>
/// Class 9000, a map: the whole galaxy (mode 0) or the current star system
/// (mode 1). The galaxy always fits the pane; there is no zoom or scroll.
/// A click picks a star or an object; with a fleet of the player selected,
/// the right button or an order mode gives the fleet an order instead.
/// </summary>
public sealed class MapPane(ViewNode node, int left, int top, ByteOptionDetails details) : Pane(node, left, top)
{
    /// <summary>Sent with the star's index when a click makes a star current (the player's 1003).</summary>
    public const int StarMadeCurrent = 1003;

    /// <summary>Sent when the map has handled an order, so the window clears the order modes (1014).</summary>
    public const int OrderHandled = 1014;

    /// <summary>A double click's action at release, sent as (map id, kind): 1 battle, 2 fleet, 3 planet, 4 wormhole, 5 star.</summary>
    public const int DoubleFleet = 2;
    public const int DoublePlanet = 3;
    public const int DoubleWormhole = 4;
    public const int DoubleStar = 5;
    public const int DoubleMarker = 1;

    private const int FrameTime = 40;
    private const int NameDelay = 400;
    private const int CombatFrameTime = 100;
    private const int StarHitRange = 40;

    private long _now;
    private long _shownSince = -1;
    private int _hovered = -1;
    private object? _hoverObject;
    private (int X, int Y) _pointer;
    private long _hoverSince;
    private bool _showName;
    private int _pendingDouble;
    private long _flashStart = -1;
    private SystemView? _system;

    /// <summary>0 the galaxy, 1 the system (+0xA4).</summary>
    public int ViewMode { get; set; } = details.Value;

    /// <summary>The big pane look: a nebula behind, large pictures, names (+0xA8).</summary>
    public bool BigLook { get; set; }

    /// <summary>The bridge's colors (+0xB4).</summary>
    public bool AltColors { get; set; }

    /// <summary>The port's own: a big pane shows a sky made for its system (`SystemSky`) instead of the game's nebula pictures.</summary>
    public bool MadeSky { get; set; }

    private (uint Seed, int Width, int Height, object Palette, byte[] Pixels)? _sky;

    /// <summary>0 none, 1 move, 2 attack, 3 colonize (+0xB0), set by the fleet buttons; the pointer follows.</summary>
    public int OrderMode
    {
        get;
        set
        {
            field = value;
            ShowCursor();
        }
    }

    public GameSession? Session { get; set; }

    internal SystemView System => _system ??= new SystemView(this, Assets);

    /// <summary>
    /// How far in from each side the map's content sits (docs/ui/widescreen.md):
    /// a widened map keeps its original shape, centered, while its pane (and
    /// so its background and clicks) takes the whole width. 0 at 640.
    /// </summary>
    public int ContentInset { get; set; }

    /// <summary>The same from the top and bottom, for a map grown in height.</summary>
    public int ContentInsetY { get; set; }

    /// <summary>
    /// After the pane has grown (docs/ui/widescreen.md): its content keeps
    /// the original's shape, as large as fits the new size, centered, since
    /// map positions would otherwise stretch with the pane.
    /// </summary>
    public void KeepShape()
    {
        if (Original is not { } o)
        {
            return;
        }

        var factor = Math.Min((double)Width / o.Width, (double)Height / o.Height);
        ContentInset = (Width - (int)(o.Width * factor)) / 2;
        ContentInsetY = (Height - (int)(o.Height * factor)) / 2;
    }

    /// <summary>Where the content starts across and down, and how large it is.</summary>
    internal int ContentLeft => Left + ContentInset;

    internal int ContentWidth => Math.Max(1, Width - (2 * ContentInset));

    internal int ContentTop => Top + ContentInsetY;

    internal int ContentHeight => Math.Max(1, Height - (2 * ContentInsetY));

    /// <summary>Galaxy coordinates (0 to 450) to the pane, scaled separately in x and y.</summary>
    public (int X, int Y) ToPane(int gx, int gy)
    {
        return (ContentLeft + ContentWidth * gx / 450, ContentTop + ContentHeight * gy / 450);
    }

    /// <summary>System coordinates (0 to 441) to the pane.</summary>
    public (int X, int Y) ToSystemPane(int px, int py)
    {
        return System.ToPane(px, py);
    }

    /// <summary>
    /// The relation color (used for names and markers): own 0x16; else by
    /// the treaty level toward the owner, war 0x12, 1 to 3 0x10, 4 to 6 0x16,
    /// never met 0xFF.
    /// </summary>
    public static byte RelationColor(Player viewer, Player owner)
    {
        var level = owner == viewer ? 6 : viewer.Treaties[owner.Index].Level();
        return level switch
        {
            Treaty.None => 0xFF,
            < 1 => 0x12,
            <= 3 => 0x10,
            _ => 0x16,
        };
    }

    protected internal override void Tick(long now)
    {
        _now = now;
        if (_shownSince < 0)
        {
            _shownSince = now;
        }

        if ((_hovered >= 0 || _hoverObject is not null) && !_showName && now - _hoverSince >= NameDelay)
        {
            _showName = true;
        }
    }

    protected internal override void MouseWithin(int x, int y)
    {
        if (Session is not { } session)
        {
            return;
        }

        if (ViewMode == 0)
        {
            var star = StarAt(x, y);
            if (star != _hovered)
            {
                (_hovered, _hoverSince, _showName) = (star, _now, false);
            }
        }
        else
        {
            _pointer = (x, y);
            var (px, py) = System.FromPane(x, y);
            var hit = System.HitTest(session.Current, px, py);
            if (hit != _hoverObject)
            {
                (_hoverObject, _hoverSince, _showName) = (hit, _now, false);
            }
        }

        ShowCursor();
    }

    /// <summary>
    /// The pointer for the order waiting: the arrow with none,
    /// cross hairs to move, and for attack and colonize one picture over a
    /// fitting target in the system and another elsewhere. Quirk kept: with
    /// no order the spinner's slide pointer is left alone.
    /// </summary>
    private void ShowCursor()
    {
        var cursor = Assets.Cursor;
        var local = Session?.Local;
        switch (OrderMode)
        {
            case 0 when cursor.Set != MouseCursor.Slide:
                cursor.Show(MouseCursor.Arrow);
                break;
            case 1:
                cursor.Show(MouseCursor.Target);
                break;
            case 2:
                // another's fleet, or a planet with another's colony.
                var enemy = _hoverObject switch
                {
                    Fleet f => f.Owner != local,
                    Planet { Colony: { } c } => c.Owner != local,
                    _ => false,
                };
                cursor.Show(enemy ? MouseCursor.Attack : MouseCursor.CannotAttack);
                break;
            case 3:
                // a free planet that is not Class 5.
                cursor.Show(_hoverObject is Planet { Colony: null, Class: not 5 } ? MouseCursor.Colonize : MouseCursor.CannotColonize);
                break;
        }
    }

    /// <summary>
    /// The port's own: with ATTACK waiting and the pointer on another's
    /// planet or fleet, the chance of the attack's success beside the
    /// pointer, when a mod gives one (Game.RateAttack, docs/modding.md).
    /// </summary>
    private void DrawAttackOdds(Framebuffer target, GameSession session)
    {
        if (OrderMode != 2 || session.Selection is not Fleet own || own.Owner != session.Local || session.Game.RateAttack is not { } rate)
        {
            return;
        }

        ISpaceObject? aim = _hoverObject switch
        {
            Fleet f when f.Owner != own.Owner => f,
            Planet { Colony: { } c } p when c.Owner != own.Owner => p,
            _ => null,
        };
        if (aim is null || rate(session.Game, own, aim) is not { } chance)
        {
            return;
        }

        var label = string.Format(CultureInfo.InvariantCulture, UiText.AttackOdds, Math.Round(Math.Clamp(chance, 0, 1) * 100));
        var color = chance >= 0.66 ? (byte)0x16 : chance >= 0.33 ? (byte)0x10 : (byte)0x12; // the relation colors: own, friendly, war
        Assets.Fonts.ForIndex(1).Draw(target, label, _pointer.X + 18, _pointer.Y + 6, 0, 0, color, 0);
    }

    protected internal override void MouseLeft()
    {
        (_hovered, _hoverObject, _showName) = (-1, null, false);
    }

    /// <summary>An order if one applies, else a pick: a double click or a single one.</summary>
    protected internal override void MouseDown(int x, int y, int clicks)
    {
        if (Session is not { } session || Order(session, x, y))
        {
            return;
        }

        if (clicks >= 2)
        {
            DoubleClick(session, x, y);
        }
        else
        {
            SingleClick(session, x, y);
        }
    }

    /// <summary>
    /// A double click acts at release. Quirk kept: the action is
    /// never cleared, so later releases on this map repeat it.
    /// </summary>
    protected internal override void MouseUp(int x, int y)
    {
        if (_pendingDouble != 0)
        {
            Post(Id, _pendingDouble);
        }
    }

    private void SingleClick(GameSession session, int x, int y)
    {
        if (ViewMode == 0)
        {
            if (StarAt(x, y) is >= 0 and var star)
            {
                session.SetCurrentStar(star);
                Post(StarMadeCurrent, star);
            }

            return;
        }

        // Wormholes, fleets out of view and gas giants cannot be picked.
        var sight = session.Current.Sight[session.Local.Index];
        if (sight != Star.Unseen && Pickable(SystemHit(session, x, y), sight) is { } picked and not Wormhole)
        {
            session.Select(picked);
        }
    }

    private void DoubleClick(GameSession session, int x, int y)
    {
        if (ViewMode == 0)
        {
            if (StarAt(x, y) is >= 0 and var star)
            {
                session.SetCurrentStar(star);
                Post(StarMadeCurrent, star);
                _pendingDouble = DoubleStar;
            }

            return;
        }

        var sight = session.Current.Sight[session.Local.Index];

        // a battle's marker is looked for first; at release the battle window opens.
        var (mx, my) = System.FromPane(x, y);
        if (sight != Star.Unseen && System.MarkerAt(session, mx, my) is not null)
        {
            _pendingDouble = DoubleMarker;
            return;
        }

        switch (sight == Star.Unseen ? null : Pickable(SystemHit(session, x, y), sight))
        {
            case Fleet fleet:
                session.Select(fleet);
                _pendingDouble = DoubleFleet;
                break;
            case Planet planet:
                session.Select(planet);
                _pendingDouble = DoublePlanet;
                break;
            case Wormhole { Partner: { } partner }:
                session.SetCurrentStar(partner.Star);
                Post(StarMadeCurrent, partner.Star);
                _pendingDouble = DoubleWormhole;
                break;
            case Star star:
                session.Select(star);
                _pendingDouble = DoubleStar;
                break;
        }
    }

    private static object? Pickable(object? hit, int sight)
    {
        return hit switch
        {
            Fleet when sight != Star.InView => null,
            Planet { Class: 5 } => null,
            _ => hit,
        };
    }

    private object? SystemHit(GameSession session, int x, int y)
    {
        var (px, py) = System.FromPane(x, y);
        return System.HitTest(session.Current, px, py);
    }

    /// <summary>
    /// Gives the selected fleet an order when the right button is used or an
    /// order mode is on. A voice answers, picked by chance
    /// outside the game's random numbers, as the original picks it.
    /// </summary>
    private bool Order(GameSession session, int x, int y)
    {
        if (session.Selection is not Fleet fleet || fleet.Owner != session.Local || (!(Window?.RightButton ?? false) && OrderMode == 0))
        {
            return false;
        }

        var game = session.Game;
        if (ViewMode == 0)
        {
            // refused for no star, or in the attack and colonize modes.
            var routed = StarAt(x, y) is >= 0 and var star && OrderMode is not (2 or 3)
                && Route(session, fleet, star, FleetOrderKind.Move);
            if (routed && session.LeftOutFleet == fleet)
            {
                session.LeftOut.Clear();
            }

            Answer(routed);
        }
        else if (fleet.Star == session.CurrentStar)
        {
            Flash(SystemOrder(session, fleet, x, y, true));
        }
        else
        {
            var ok = Route(session, fleet, session.CurrentStar, Kind()) && SystemOrder(session, fleet, x, y, false);
            if (session.LeftOutFleet == fleet && ok)
            {
                session.LeftOut.Clear();
            }
            Flash(ok);
        }

        Post(OrderHandled, 0);
        return true;
    }

    private void Answer(bool accepted)
    {
        var voices = accepted ? Audio.SoundIds.MapAccepted : Audio.SoundIds.MapRefused;
        PlaySound(voices[Random.Shared.Next(voices.Length)]);
    }

    private void Flash(bool accepted)
    {
        Answer(accepted);
        _flashStart = accepted ? _now : -1;
    }

    private FleetOrderKind Kind()
    {
        return OrderMode switch
        {
            2 => FleetOrderKind.Attack,
            3 => FleetOrderKind.Colonize,
            _ => FleetOrderKind.Move,
        };
    }

    /// <summary>
    /// A fleet sent to another star, the ships left out split
    /// off first; refused, with nothing sent, when its fuel does not reach.
    /// </summary>
    private static bool Route(GameSession session, Fleet fleet, int star, FleetOrderKind kind)
    {
        if (!session.Game.CanReach(fleet, star))
        {
            return false;
        }

        int[] splitOff = session.LeftOutFleet == fleet ? [.. session.LeftOut] : [];
        session.Send(new FleetRouteCommand(session.Local.Index, fleet.ObjectId, star, kind, true, splitOff));
        return true;
    }

    /// <summary>An order to what is under the point in the system; false when refused.</summary>
    private bool SystemOrder(GameSession session, Fleet fleet, int x, int y, bool replace)
    {
        var game = session.Game;
        var (px, py) = System.FromPane(x, y);
        var target = System.HitTest(session.Current, px, py);
        void Give(FleetOrderKind kind, Order order)
        {
            // every accepted order splits off the ships left out first.
            session.SplitOff(fleet);
            var (ox, oy) = order.Position;
            var id = game.ObjectIdOf(order.Target);
            session.Send(new FleetOrderCommand(session.Local.Index, fleet.ObjectId, kind, id, ox, oy, replace));
        }

        switch (target)
        {
            case Fleet other when other == fleet:
                return false;
            case Fleet other:
                if (OrderMode == 2 && other.Owner != fleet.Owner)
                {
                    Give(FleetOrderKind.Attack, Sim.Players.Order.To(other));
                    return true;
                }

                if (OrderMode is 2 or 3)
                {
                    return false;
                }

                Give(FleetOrderKind.Move, Sim.Players.Order.To(other));
                return true;
            case Planet planet:
                if (OrderMode == 3)
                {
                    if (planet.Colony is not null || planet.Class == 5)
                    {
                        return false;
                    }

                    Give(FleetOrderKind.Colonize, Sim.Players.Order.To(planet));
                    return true;
                }

                if (OrderMode == 2)
                {
                    if (planet.Owner is not { } owner || owner == fleet.Owner)
                    {
                        return false;
                    }

                    Give(FleetOrderKind.Attack, Sim.Players.Order.To(planet));
                    return true;
                }

                Give(FleetOrderKind.Move, Sim.Players.Order.To(planet));
                return true;
            case Wormhole hole when OrderMode is 0 or 1:
                if (hole.Partner is not { } partner || !game.CanReach(fleet, partner.Star))
                {
                    return false;
                }

                Give(FleetOrderKind.Move, Sim.Players.Order.To(hole));
                return true;
            case Star star when OrderMode is 0 or 1:
                Give(FleetOrderKind.Move, Sim.Players.Order.To(star.Center));
                return true;
            case null when OrderMode is 0 or 1:
                Give(FleetOrderKind.Move, Sim.Players.Order.ToPoint(px, py));
                return true;
            default:
                return false;
        }
    }

    /// <summary>The nearest star within 40 galaxy units, the first on ties.</summary>
    private int StarAt(int x, int y)
    {
        if (Session is not { } session)
        {
            return -1;
        }

        var gx = (x - ContentLeft) * 450 / ContentWidth;
        var gy = (y - ContentTop) * 450 / ContentHeight;
        var best = -1;
        var bestDistance = StarHitRange;
        foreach (var star in session.Game.Universe.Stars)
        {
            var d = (int)Math.Sqrt(((star.X - gx) * (star.X - gx)) + ((star.Y - gy) * (star.Y - gy)));
            if (d < bestDistance)
            {
                (best, bestDistance) = (star.Index, d);
            }
        }

        return best;
    }

    /// <summary>The system's own sky at the frame's pixels, painted once for each system, size and palette and kept.</summary>
    private void DrawMadeSky(Framebuffer target, string star, Data.Art.Palette palette)
    {
        var (seed, w, h) = (SystemSky.Seed(star), Width * target.Scale, Height * target.Scale);
        if (_sky is not { } sky || sky.Seed != seed || sky.Width != w || sky.Height != h || !ReferenceEquals(sky.Palette, palette))
        {
            _sky = sky = (seed, w, h, palette, SystemSky.Paint(seed, w, h, palette));
        }

        target.DrawNative(sky.Pixels, w, h, Left, Top);
    }

    protected override void Draw(Framebuffer target)
    {
        if (Session is not { } session)
        {
            return;
        }

        if (BigLook && MadeSky && target.Palette is { } palette && session.CurrentStar >= 0 && session.CurrentStar < session.Game.Universe.Stars.Count)
        {
            DrawMadeSky(target, session.Game.Universe.Stars[session.CurrentStar].Name, palette);
        }

        // the nebula behind a big pane, else the window's own art shows.
        // A pane grown in a large window (docs/ui/widescreen.md) is filled
        // with it, every other copy across mirrored so the joins do not show.
        else if (BigLook && Assets.Pictures(ImageSet.Universe).Get((session.CurrentStar % 7) + 1) is { } nebula)
        {
            if (Original is { } o && (Width > o.Width || Height > o.Height) && nebula.Width > 0 && nebula.Height > 0)
            {
                for (var (y, row) = (Top, 0); y < Top + Height; y += nebula.Height, row++)
                {
                    for (var (x, column) = (Left, 0); x < Left + Width; x += nebula.Width, column++)
                    {
                        target.DrawRegion(nebula, 0, 0, nebula.Width, nebula.Height, x, y, nebula.Width, nebula.Height, null, column % 2 == 1);
                    }
                }
            }
            else
            {
                target.Draw(nebula, Left, Top);
            }
        }

        var frame = (int)(_now / FrameTime % 10);
        if (ViewMode == 0)
        {
            DrawGalaxy(target, session, frame);
        }
        else
        {
            // The port's own: over the system's starfield and planets, outlined text is white (decisions.md, "Outlined text").
            var text = Assets.Fonts.Settings;
            var outline = text.Outline;
            if (outline != Render.TextOutline.None)
            {
                text.Outline = Render.TextOutline.White;
            }

            System.Draw(target, session, _now, _now - _shownSince);
            System.DrawMarker(target, session, frame);
            if (_showName && _hoverObject is not null && !BigLook)
            {
                DrawSystemName(target, session, _hoverObject);
            }

            DrawAttackOdds(target, session);
            text.Outline = outline;

            if (_flashStart >= 0 && session.Selection is Fleet fleet)
            {
                var count = (int)((_now - _flashStart) / FrameTime);
                System.DrawOrderFlash(target, session, fleet, count);
                if (count >= SystemView.FlashDraws)
                {
                    _flashStart = -1;
                }
            }
        }

        if (BigLook && Assets.Pictures(ImageSet.Main).Get(13011) is { } corner)
        {
            target.Draw(corner, Left + Width - corner.Width, Top + Height - corner.Height);
        }
    }

    private void DrawGalaxy(Framebuffer target, GameSession session, int frame)
    {
        var stars = session.Game.Universe.Stars;
        var local = session.Local;
        var current = session.Current;

        // the marker steps a frame each 40 ms redraw.
        var markerBase = (BigLook ? 1000 : 1050) + (AltColors ? 50 : 0);
        var (cx, cy) = ToPane(current.X, current.Y);
        DrawCentered(target, Assets.Pictures(ImageSet.Infra).Get(markerBase + frame), cx, cy);

        // Every seen star's wormholes, then the current star's in their own color.
        foreach (var star in stars)
        {
            if (star != current && star.Sight[local.Index] != Star.Unseen)
            {
                DrawLinks(target, star, stars, AltColors ? (byte)0x1D : (byte)0x17);
            }
        }

        if (current.Sight[local.Index] != Star.Unseen)
        {
            DrawLinks(target, current, stars, AltColors ? (byte)0x52 : (byte)0x16);
        }

        var universe = Assets.Pictures(ImageSet.Universe);
        var alt = AltColors ? 100 : 0;
        var battles = session.Game.Battles.Select(b => b.Star).ToHashSet();
        foreach (var star in stars)
        {
            var (x, y) = ToPane(star.X, star.Y);
            if (battles.Contains(star.Index))
            {
                DrawCentered(target, universe.Get(20001 + alt + (int)(_now / CombatFrameTime % 5)), x, y);
                continue;
            }

            DrawCentered(target, universe.Get(StarPicture(star, local) + alt), x, y);
            if (star.Sight[local.Index] == Star.InView)
            {
                foreach (var icon in FleetIcons(star, local))
                {
                    DrawCentered(target, universe.Get(icon + alt), x, y);
                }
            }
        }

        DrawRoute(target, session);
        if (_showName && _hovered >= 0)
        {
            DrawStarName(target, stars[_hovered], local);
        }
    }

    /// <summary>The selected fleet's way to the hovered star, each hop colored by whether fuel holds.</summary>
    private void DrawRoute(Framebuffer target, GameSession session)
    {
        if (_hovered < 0 || session.Selection is not Fleet fleet || fleet.Owner != session.Local || fleet.Star == _hovered)
        {
            return;
        }

        var stars = session.Game.Universe.Stars;
        foreach (var (from, to, fine) in session.Game.RouteHops(fleet, _hovered, session.Local))
        {
            var (x0, y0) = ToPane(stars[from].X, stars[from].Y);
            var (x1, y1) = ToPane(stars[to].X, stars[to].Y);
            var color = fine ? (AltColors ? (byte)0xB2 : (byte)0x10) : (AltColors ? (byte)0xDF : (byte)0x12);
            target.DrawLine(x0, y0, x1, y1, color);
        }
    }

    private void DrawLinks(Framebuffer target, Star star, IReadOnlyList<Star> stars, byte color)
    {
        var (x0, y0) = ToPane(star.X, star.Y);
        foreach (var hole in star.Wormholes)
        {
            if (hole.Partner is { } partner)
            {
                var far = stars[partner.Star];
                var (x1, y1) = ToPane(far.X, far.Y);
                target.DrawLine(x0, y0, x1, y1, color);
            }
        }
    }

    /// <summary>
    /// The star's map picture: unseen 20000, remembered 20009;
    /// in view 20010 plus the worst relation with its planets' other owners
    /// (1 friendly, 2 neutral or never met, 3 at war), or 20020 plus that
    /// when the player owns a planet there.
    /// </summary>
    private static int StarPicture(Star star, Player local)
    {
        switch (star.Sight[local.Index])
        {
            case Star.Unseen:
                return 20000;
            case Star.Remembered:
                return 20009;
        }

        var worst = 0;
        foreach (var owner in star.Planets.Select(p => p.Owner).OfType<Player>().Where(o => o != local))
        {
            worst = Math.Max(worst, Relation(local, owner));
        }

        return (star.Planets.Any(p => p.Owner == local) ? 20020 : 20010) + worst;
    }

    /// <summary>1 at levels 4 to 6, 2 at 1 to 3 or never met, 3 at war.</summary>
    private static int Relation(Player local, Player other)
    {
        var level = local.Treaties[other.Index].Level();
        return level is >= 4 and <= 6 ? 1 : level < 1 ? 3 : 2;
    }

    /// <summary>The fleet marks over a star in view: own, friendly, neutral, hostile ( and its neighbors).</summary>
    private static IEnumerable<int> FleetIcons(Star star, Player local)
    {
        if (star.Fleets.Any(f => f.Owner == local))
        {
            yield return 20030;
        }

        var others = star.Fleets.Where(f => f.Owner != local).Select(f => Relation(local, f.Owner)).ToHashSet();
        foreach (var relation in new[] { 1, 2, 3 })
        {
            if (others.Contains(relation))
            {
                yield return 20030 + relation;
            }
        }
    }

    private void DrawStarName(Framebuffer target, Star star, Player local)
    {
        var seen = star.Sight[local.Index] != Star.Unseen;
        var color = seen ? (AltColors ? (byte)0x52 : (byte)0x16) : (AltColors ? (byte)0xB3 : (byte)0x10);
        var (x, y) = ToPane(star.X, star.Y);
        DrawName(target, star.Name, x, y, color, BigLook ? 4 : 1);
    }

    private void DrawSystemName(Framebuffer target, GameSession session, object hovered)
    {
        var local = session.Local;
        var stars = session.Game.Universe.Stars;
        var (name, x, y, color) = hovered switch
        {
            Planet p => (p.Name, p.X, p.Y, p.Colony is null ? (byte)0x19 : p.Owner == local ? (byte)0x16 : (byte)0x12),
            Fleet f => (f.Name, f.X, f.Y, f.Owner == local ? (byte)0x16 : (byte)0x12),
            Wormhole { Partner: { } far } w => (w.Name, w.X, w.Y, stars[far.Star].Sight[local.Index] == Star.Unseen ? (byte)0x19 : (byte)0x16),
            Star s => (s.Name, 220, 220, s.Sight[local.Index] == Star.Unseen ? (byte)0x19 : (byte)0x16),
            _ => ("", 0, 0, (byte)0),
        };
        var (px, py) = System.ToPane(x, y);
        DrawName(target, name, px, py, color, 1);
    }

    private void DrawName(Framebuffer target, string name, int x, int y, byte color, int fontIndex)
    {
        var font = Assets.Fonts.ForIndex(fontIndex);
        x += 2;
        var width = font.Measure(name, 0);
        if (x + width > Left + Width)
        {
            x = Left + Width - width - 2;
        }

        if (y < Top + font.LineHeight)
        {
            y = Top + font.LineHeight;
        }

        font.Draw(target, name, x, y, 0, 0, color, 0);
    }

    internal static void DrawCentered(Framebuffer target, Sprite? sprite, int x, int y)
    {
        if (sprite is not null)
        {
            target.Draw(sprite, x - sprite.Width / 2, y - sprite.Height / 2);
        }
    }
}
