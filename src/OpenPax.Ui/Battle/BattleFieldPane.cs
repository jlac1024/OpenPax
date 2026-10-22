using OpenPax.Data.Views;
using OpenPax.Render;
using OpenPax.Sim.Combat;
using OpenPax.Sim.Commands;
using OpenPax.Sim.Numerics;
using OpenPax.Sim.Players;
using OpenPax.Ui.Widgets;

namespace OpenPax.Ui.Battle;

/// <summary>
/// The battle field (class 24001): the 3600 square field seen
/// from 35 degrees, scrolled but never zoomed. It draws the battle as it
/// stands, picks and selects the local player's ships, and turns clicks and
/// keys into battle orders. It never changes the battle itself.
/// </summary>
public sealed class BattleFieldPane(ViewNode node, int left, int top) : Pane(node, left, top)
{
    /// <summary>To the window: the selection changed, with its count (0x3FD).</summary>
    public const int SelectionMessage = 0x3FD;

    /// <summary>To the window: a command mode was chosen (0x3FE).</summary>
    public const int ModeMessage = 0x3FE;

    public const int FullHeight = 423;
    public const int BarHeight = 335;
    public const int FieldWidth = 3600;
    public const int FieldHeight = 2065;
    public const int SelectionLimit = 150;

    private const double Down = 0.573576436351046;
    private const double Up = 1.743446795;
    private const int AnimationMs = 83;
    private const int PlanetX = 3230;
    private const int PlanetY = 1772;
    private const int BackdropY = 1765;

    private static readonly (int X, int Y)[] _blockSpots =
    [
        (166, 152), (220, 91), (124, 187), (178, 144), (155, 180),
        (211, 125), (188, 135), (280, 94), (124, 226), (226, 124),
    ];

    private static readonly (int X, int Y)[] _craftOffsets = [(-10, 0), (13, -13), (13, 13)];
    private static readonly int[] _healthWidths = [31, 41, 31, 61, 71, 51];

    private readonly Dictionary<BattleObject, (long Last, int Frame)> _animations = [];
    private readonly Dictionary<Shot, int> _shotDraws = [];
    private readonly HashSet<Shot> _heard = [];
    private (int X, int Y)[]? _spread;
    private long _now;
    private int _height = BarHeight;
    private (int X, int Y) _anchor;
    private (int X, int Y) _corner;
    private bool _dragging;

    public GameSession? Session { get; set; }

    /// <summary>The battle shown (+0xA4), or null.</summary>
    public Sim.Combat.Battle? Battle { get; private set; }

    /// <summary>The order the pane wants given; the window passes it to the game.</summary>
    public Action<BattleOrder>? Send { get; set; }

    public int ScrollX { get; private set; }

    public int ScrollY { get; private set; }

    /// <summary>The last ship or orbital clicked (+0xA8).</summary>
    public BattleObject? Tracked { get; private set; }

    /// <summary>The local player's ships chosen, the last added first (+0xAC).</summary>
    public List<BattleShip> Selection { get; } = [];

    /// <summary>The command waiting for a click on the field (+0xC8), 0 for none.</summary>
    public int Mode { get; private set; }

    /// <summary>Scroll steps for the arrow keys and the field's edge (+0xCC, +0xD0; the edge never fires in this layout).</summary>
    public int KeyStep { get; set; } = 15;

    public override int Height => _height;

    private int LocalIndex => Session?.Local.Index ?? 0;

    private IReadOnlyList<Player> Players => Session?.Game.Players ?? [];

    /// <summary>Shows a battle, or none.</summary>
    public void Attach(Sim.Combat.Battle? battle)
    {
        Battle = battle;
        Tracked = null;
        Selection.Clear();
        Mode = 0;
        _animations.Clear();
        _shotDraws.Clear();
    }

    /// <summary>The control bar takes the bottom of the field, or not.</summary>
    public void SetHeight(int height)
    {
        _height = height;
    }

    /// <summary>A field point to screen: the depth is foreshortened.</summary>
    public (int X, int Y) ToScreen(int x, int y)
    {
        return (x - ScrollX + Left, (int)(y * Down + 0.5) - ScrollY + Top);
    }

    private (int X, int Y) ToField(int x, int y)
    {
        return (x - Left + ScrollX, (int)((y - Top + ScrollY) * Up + 0.5));
    }

    /// <summary>Scrolls, kept inside the field.</summary>
    public void ScrollTo(int x, int y)
    {
        ScrollX = Math.Clamp(x, 0, FieldWidth - Width);
        ScrollY = Math.Clamp(y, 0, FieldHeight - _height);
    }

    /// <summary>Centers on a field point.</summary>
    public void CenterOn(int x, int y)
    {
        ScrollTo(x - (Width >> 1), (int)(y * Down + 0.5) - (_height >> 1));
    }

    /// <summary>Opens on the local side: its newest ship, else orbital, else its planet's corner, else near the start.</summary>
    public void CenterOnLocalSide()
    {
        if (Battle is not { } b)
        {
            return;
        }

        var p = LocalIndex;
        if (b.Ships[p].FirstOrDefault() is { } ship)
        {
            CenterOn(ship.X, ship.Y);
        }
        else if (b.Orbitals[p].FirstOrDefault() is { } orbital)
        {
            CenterOn(orbital.X, orbital.Y);
        }
        else if (b.Planet?.Colony?.Owner.Index == p)
        {
            CenterOn(3600, 3600);
        }
        else
        {
            CenterOn(1500, 1500);
        }
    }

    protected internal override void Tick(long now)
    {
        _now = now;
        if (Tracked is { Dead: true })
        {
            Tracked = null;
        }

        if (Selection.RemoveAll(s => s.Dead) > 0)
        {
            Post(SelectionMessage, Selection.Count);
        }

        foreach (var shot in _shotDraws.Keys.Where(s => s.Dead).ToList())
        {
            _shotDraws.Remove(shot);
        }

        // each new shot sounds its weapon, while the window is open.
        _heard.RemoveWhere(s => s.Dead);
        if (Battle is { } b)
        {
            foreach (var shot in b.Shots.Concat(b.Missiles).SelectMany(l => l))
            {
                if (_heard.Add(shot))
                {
                    PlaySound(BattleArt.ShotSound(shot));
                }
            }
        }
    }

    protected override void Draw(Framebuffer target)
    {
        DrawBackground(target);
        if (Battle is { } b && Session is { } session)
        {
            var combat = Assets.Pictures(ImageSet.Combat);
            if (b.PlanetObject is not null)
            {
                DrawBlocks(target, b);
            }

            foreach (var effect in b.ShipEffects)
            {
                DrawEffect(target, effect);
            }

            for (var p = 0; p < Sim.Combat.Battle.Players; p++)
            {
                var visible = b.Mines[p].Count > 0 && session.Game.MinesVisible(b, p, LocalIndex);
                foreach (var mine in b.Mines[p])
                {
                    var sprite = combat.Get(BattleArt.Mine + Animate(mine));
                    if (sprite is null)
                    {
                        continue;
                    }

                    var (x, y) = ToScreen(mine.X, mine.Y);
                    if (visible)
                    {
                        target.Draw(sprite, x - (sprite.Width >> 1), y - (sprite.Height >> 1));
                    }
                    else
                    {
                        target.DrawTinted(sprite, x - (sprite.Width >> 1), y - (sprite.Height >> 1), 0);
                    }
                }
            }

            foreach (var lists in (List<Shot>[][])[b.Shots, b.Missiles])
            {
                foreach (var shot in lists.SelectMany(l => l))
                {
                    DrawShot(target, shot);
                }
            }

            DrawBand(target);
            for (var p = 0; p < Sim.Combat.Battle.Players; p++)
            {
                foreach (var orbital in b.Orbitals[p])
                {
                    DrawOrbital(target, orbital);
                }

                var firstFighter = session.Game.Tables.OfKind(TechInfo.FighterKind).First().Id;
                foreach (var fighter in b.Fighters[p].Where(f => !f.Docked))
                {
                    DrawFighter(target, fighter, firstFighter);
                }
            }

            DrawShips(target, b);
            foreach (var effect in b.OrbitalEffects)
            {
                DrawEffect(target, effect);
            }
        }

        if (_height == FullHeight)
        {
            var main = Assets.Pictures(ImageSet.Main);
            if (main.Get(24005) is { } right)
            {
                target.Draw(right, Left + Width - right.Width, Top + FullHeight - right.Height);
            }

            if (main.Get(24004) is { } left)
            {
                target.Draw(left, Left, Top + FullHeight - left.Height);
            }
        }
    }

    /// <summary>
    /// The star tiles scrolling at half speed, then the planet's backdrop in
    /// the far corner. Quirk kept: the tiles' vertical offset
    /// adds the view's width, not its height.
    /// </summary>
    private void DrawBackground(Framebuffer target)
    {
        if (Assets.Pictures(ImageSet.Universe).Get(1) is { } tile)
        {
            var ox = ((ScrollX + Width) / 2) % tile.Width;
            var oy = ((ScrollY + Width) / 2) % tile.Height;
            for (var y = Top - oy; y < Top + _height; y += tile.Height)
            {
                for (var x = Left - ox; x < Left + Width; x += tile.Width)
                {
                    target.Draw(tile, x, y);
                }
            }
        }

        if (Battle?.Planet is { } planet && Assets.Pictures(ImageSet.Combat).Get(20500 + planet.Class) is { } backdrop)
        {
            target.Draw(backdrop, FieldWidth - backdrop.Width - ScrollX + Left, BackdropY - ScrollY + Top);
        }
    }

    /// <summary>The planet's blocks, standing or ruined, highest first, at fixed spots.</summary>
    private void DrawBlocks(Framebuffer target, Sim.Combat.Battle b)
    {
        var combat = Assets.Pictures(ImageSet.Combat);
        var standing = b.Planet?.Class == 4 ? 20100 : 20200;
        foreach (var block in b.Blocks)
        {
            var picture = block.Dead ? 20300 + block.Index : standing + block.Index;
            if (combat.Get(picture) is { } sprite)
            {
                var (dx, dy) = _blockSpots[block.Index];
                target.Draw(sprite, PlanetX + dx - ScrollX + Left, PlanetY + dy - ScrollY + Top);
            }
        }
    }

    /// <summary>An effect sounds when first drawn at age 1, so a slow frame can miss it, as in the original.</summary>
    private void DrawEffect(Framebuffer target, Effect effect)
    {
        if (effect.Age == 1)
        {
            PlaySound(BattleArt.EffectSound(effect));
        }

        DrawCentered(target, Assets.Pictures(ImageSet.Combat).Get(BattleArt.Effect(effect)), effect);
    }

    private void DrawCentered(Framebuffer target, Sprite? sprite, BattleObject o)
    {
        if (sprite is not null)
        {
            var (x, y) = ToScreen(o.X, o.Y);
            target.Draw(sprite, x - (sprite.Width >> 1), y - (sprite.Height >> 1));
        }
    }

    /// <summary>One of 32 frames, a frame each 83 ms while it is drawn.</summary>
    private int Animate(BattleObject o)
    {
        if (!_animations.TryGetValue(o, out var a))
        {
            a = (_now, 0);
        }
        else if (_now - a.Last >= AnimationMs)
        {
            a = (_now, a.Frame + 1 > 31 ? 0 : a.Frame + 1);
        }

        _animations[o] = a;
        return a.Frame;
    }

    /// <summary>
    /// A shot's rounds side by side across its line of flight:
    /// 8 apart, out to each side in turn, in steps of 22.5 degrees.
    /// </summary>
    private void DrawShot(Framebuffer target, Shot shot)
    {
        var draws = _shotDraws.GetValueOrDefault(shot) + 1 & 0xFF;
        _shotDraws[shot] = draws;
        if (Assets.Pictures(ImageSet.Combat).Get(BattleArt.Shot(shot, draws)) is not { } sprite)
        {
            return;
        }

        var spread = Spread();
        var (x, y) = ToScreen(shot.X, shot.Y);
        for (var i = 0; i < shot.Rounds; i++)
        {
            var k = i + shot.SpreadGroup * 11;
            var (dx, dy) = k < spread.Length ? spread[k] : (0, 0);
            target.Draw(sprite, x + dx - (sprite.Width >> 1), y + dy - (sprite.Height >> 1));
        }
    }

    /// <summary>The spread table: 8 groups of 11 offsets, built from the game's sine and cosine.</summary>
    private (int X, int Y)[] Spread()
    {
        if (_spread is not null)
        {
            return _spread;
        }

        var tables = Session!.Game.Tables; // drawing a battle needs a game
        _spread = new (int X, int Y)[8 * 11];
        for (var k = 0; k < 8; k++)
        {
            var a = (int)(k * 22.5);
            for (var i = 1; i < 11; i++)
            {
                var angle = i % 2 == 1 ? a : a + 180;
                var r = (i + 1) / 2 * 8;
                _spread[k * 11 + i] = ((int)(tables.Cos[angle] * r - -0.5), (int)(tables.Sin[angle] * r - -0.5));
            }
        }

        return _spread;
    }

    private void DrawOrbital(Framebuffer target, Orbital orbital)
    {
        var first = BattleArt.Orbital(orbital);
        var frame = Animate(orbital);
        var combat = Assets.Pictures(ImageSet.Combat);
        if (orbital.Dead || first == 0 || combat.Get(first + frame) is not { } sprite)
        {
            return;
        }

        var (x, y) = ToScreen(orbital.X, orbital.Y);
        var (l, t) = (x - (sprite.Width >> 1), y - (sprite.Height >> 1));
        target.Draw(sprite, l, t);
        if (orbital != Tracked)
        {
            return;
        }

        var color = BattleArt.Relation(Players, LocalIndex, orbital.Owner);
        if (combat.Get(first + frame + BattleArt.Mask) is { } mask)
        {
            target.DrawTinted(mask, l, t, color);
        }

        DrawHealth(target, x - 35, y + (sprite.Height >> 1), 71, (int)(orbital.Structure.Percent * 0.01 * 71), color);
    }

    /// <summary>A flight's craft at fixed offsets that do not turn with it.</summary>
    private void DrawFighter(Framebuffer target, Fighter fighter, int firstFighter)
    {
        if (Assets.Pictures(ImageSet.Combat).Get(BattleArt.Fighter(fighter, firstFighter) + BattleArt.HeadingFrame(fighter.Facing, false)) is not { } sprite)
        {
            return;
        }

        var (x, y) = ToScreen(fighter.X, fighter.Y);
        for (var k = 0; k < fighter.Craft; k++)
        {
            var (dx, dy) = _craftOffsets[k];
            target.Draw(sprite, x + dx - (sprite.Width >> 1), y + dy - (sprite.Height >> 1));
        }
    }

    /// <summary>
    /// Ships by the field's grid, column by column from just left of the
    /// view, each from its footprint's corner cell.
    /// </summary>
    private void DrawShips(Framebuffer target, Sim.Combat.Battle b)
    {
        var ships = Enumerable.Range(0, Sim.Combat.Battle.Players).SelectMany(p => b.Ships[p]).ToDictionary(s => s.Id);
        var first = Math.Max(0, (ScrollX - 200) / Sim.Combat.Battle.CellSize);
        var last = Math.Min(Sim.Combat.Battle.GridSize - 1, (ScrollX + Width) / Sim.Combat.Battle.CellSize);
        for (var c = first; c <= last; c++)
        {
            for (var r = 0; r < Sim.Combat.Battle.GridSize; r++)
            {
                var id = b.Cell(c, r);
                if (id > 0 && ships.TryGetValue(id, out var ship))
                {
                    DrawShip(target, ship, ship == Tracked || Selection.Contains(ship));
                }
            }
        }
    }

    private void DrawShip(Framebuffer target, BattleShip ship, bool highlight)
    {
        if (ship.Dead)
        {
            return;
        }

        var combat = Assets.Pictures(ImageSet.Combat);
        var color = Players[ship.Owner].FleetColor;
        if (combat.Get(BattleArt.Ship(ship, color, ship.Speed > 0.0)) is not { } sprite)
        {
            return;
        }

        var (x, y) = ToScreen(ship.X, ship.Y);
        var (l, t) = (x - (sprite.Width >> 1), y - (sprite.Height >> 1));
        target.Draw(sprite, l, t);
        if (!highlight || combat.Get(BattleArt.Ship(ship, color, false) + BattleArt.Mask) is not { } mask)
        {
            return;
        }

        var relation = BattleArt.Relation(Players, LocalIndex, ship.Owner);
        target.DrawTinted(mask, l, t, relation);
        Assets.Fonts.ForIndex(6).Draw(target, ship.Ship.Name, l, t + 10, 0, 0, 255, 0);
        var w = _healthWidths[Math.Clamp(ship.Ship.Type, 0, _healthWidths.Length - 1)];
        var pct = ship.Ship.MaxStructure < 1 ? 0 : ship.Ship.Structure * 100 / ship.Ship.MaxStructure;
        DrawHealth(target, x - (w >> 1), t + mask.Height, w, (int)(pct * 0.01 * w + 0.5), relation);
    }

    /// <summary>A health bar: the back in color 4, the part left in the relation color, each clipped to its width.</summary>
    private void DrawHealth(Framebuffer target, int x, int y, int width, int fill, byte color)
    {
        var combat = Assets.Pictures(ImageSet.Combat);
        var clip = target.Clip;
        void Clipped(int picture, int w, byte c)
        {
            if (combat.Get(picture) is not { } bar)
            {
                return;
            }

            var l = Math.Max(clip.X, x);
            var r = Math.Min(clip.X + clip.Width, x + w);
            if (r > l)
            {
                target.SetClip(l, clip.Y, r - l, clip.Height);
                target.DrawTinted(bar, x, y, c);
            }
        }

        Clipped(BattleArt.HealthBack, width, 4);
        Clipped(BattleArt.HealthFill, fill, color);
        target.SetClip(clip.X, clip.Y, clip.Width, clip.Height);
    }

    /// <summary>The rubber band in color 28.</summary>
    private void DrawBand(Framebuffer target)
    {
        if (!_dragging)
        {
            return;
        }

        var a = ToScreen(_anchor.X, _anchor.Y);
        var b = ToScreen(_corner.X, _corner.Y);
        var (l, r) = (Math.Min(a.X, b.X), Math.Max(a.X, b.X));
        var (t, bottom) = (Math.Min(a.Y, b.Y), Math.Max(a.Y, b.Y));
        if (r <= l || bottom <= t)
        {
            return;
        }

        target.DrawLine(l, t, r - 1, t, 28);
        target.DrawLine(l, bottom - 1, r - 1, bottom - 1, 28);
        target.DrawLine(l, t, l, bottom - 1, 28);
        target.DrawLine(r - 1, t, r - 1, bottom - 1, 28);
    }

    /// <summary>The ship or orbital at a field point: ships before orbitals, player by player.</summary>
    private BattleObject? Pick((int X, int Y) at)
    {
        if (Battle is not { } b)
        {
            return null;
        }

        for (var p = 0; p < Sim.Combat.Battle.Players; p++)
        {
            foreach (var ship in b.Ships[p])
            {
                if (!ship.Dead && Distance(ship, at) < ship.Radius >> 1)
                {
                    return ship;
                }
            }

            foreach (var orbital in b.Orbitals[p])
            {
                if (!orbital.Dead && Distance(orbital, at) < orbital.Size >> 1)
                {
                    return orbital;
                }
            }
        }

        return null;
    }

    private static double Distance(BattleObject o, (int X, int Y) at)
    {
        var dx = o.PosX - at.X;
        var dy = o.PosY - at.Y;
        return Math.Sqrt(dx * dx + dy * dy);
    }

    private static int Owner(BattleObject o) => o switch
    {
        BattleShip s => s.Owner,
        Orbital b => b.Owner,
        _ => -1,
    };

    /// <summary>
    /// A press: a waiting command goes off at the point. Left:
    /// an own ship is chosen alone, an enemy is only marked, empty space
    /// starts a band. Right with ships chosen: empty space moves them, an
    /// own ship joins them, an enemy is attacked.
    /// </summary>
    protected internal override void MouseDown(int x, int y, int clicks)
    {
        if (Battle is null)
        {
            return;
        }

        var at = ToField(x, y);
        if (Mode != 0)
        {
            Give(at);
            Assets.Cursor.Show(MouseCursor.Arrow);
            return;
        }

        var right = Window?.RightButton == true;
        if (!right || _dragging)
        {
            var hit = Pick(at);
            Tracked = hit;
            if (hit is null)
            {
                (_anchor, _corner, _dragging) = (at, (0, 0), true);
                return;
            }

            Selection.Clear();
            if (Owner(hit) != LocalIndex)
            {
                return;
            }

            if (hit is BattleShip ship)
            {
                Selection.Add(ship);
            }

            Post(SelectionMessage, 1);
            return;
        }

        if (Selection.Count == 0)
        {
            return;
        }

        var target = Pick(at);
        Tracked = target ?? Tracked;
        if (target is null)
        {
            Mode = 1;
            Give(at);
        }
        else if (Owner(target) != LocalIndex)
        {
            Mode = 4;
            Give(at);
        }
        else if (target is BattleShip own && Selection.Count < SelectionLimit)
        {
            Selection.Insert(0, own);
        }
    }

    /// <summary>The band follows the pointer; every own ship it meets is chosen.</summary>
    protected internal override void MouseMove(int x, int y, long now)
    {
        if (!_dragging || Battle is not { } b)
        {
            return;
        }

        var at = ToField(x, y);
        if (at == _corner)
        {
            return;
        }

        _corner = at;
        var (l, r) = (Math.Min(_anchor.X, at.X), Math.Max(_anchor.X, at.X));
        var (t, bottom) = (Math.Min(_anchor.Y, at.Y), Math.Max(_anchor.Y, at.Y));
        Selection.Clear();
        foreach (var ship in b.Ships[LocalIndex].Where(s => !s.Dead))
        {
            var h = ship.Radius >> 1;
            if (ship.X + h > l && ship.X - h < r && ship.Y + h > t && ship.Y - h < bottom)
            {
                Selection.Add(ship);
            }
        }

        Post(SelectionMessage, Selection.Count);
    }

    protected internal override void MouseUp(int x, int y)
    {
        _dragging = false;
        (_anchor, _corner) = ((0, 0), (0, 0));
    }

    /// <summary>The arrow keys scroll; Backspace drops a waiting command.</summary>
    protected internal override void Key(UiKey key, bool shift, long now)
    {
        switch (key)
        {
            case UiKey.Left:
                ScrollTo(ScrollX - KeyStep, ScrollY);
                break;
            case UiKey.Right:
                ScrollTo(ScrollX + KeyStep, ScrollY);
                break;
            case UiKey.Up:
                ScrollTo(ScrollX, ScrollY - KeyStep);
                break;
            case UiKey.Down:
                ScrollTo(ScrollX, ScrollY + KeyStep);
                break;
            case UiKey.Backspace:
                Mode = 0;
                Assets.Cursor.Show(MouseCursor.Arrow);
                break;
        }
    }

    /// <summary>
    /// Typed keys: digits choose a group (0 every ship), the
    /// shifted digits give the chosen ships that group, Space centers, and
    /// letters choose commands.
    /// </summary>
    protected internal override void Typed(char c, long now)
    {
        if (Battle is not { } b)
        {
            return;
        }

        const string groupKeys = "!@#$%^&*(";
        c = char.ToLowerInvariant(c);
        if (c is >= '0' and <= '9')
        {
            var n = c - '0';
            Selection.Clear();
            Selection.AddRange(b.Ships[LocalIndex].Where(s => n == 0 || (!s.Dead && s.Ship.CombatGroup == n)));
            Post(SelectionMessage, Selection.Count);
            return;
        }

        if (groupKeys.IndexOf(c, StringComparison.Ordinal) is var g and >= 0)
        {
            Session?.Send(new BattleGroupCommand(LocalIndex, [.. Selection.Select(s => s.Ship.ShipId)], g + 1));

            return;
        }

        var mode = c switch
        {
            'm' => 1,
            'f' => 2,
            't' => 3,
            'a' => 4,
            'q' => 5,
            'w' => 6,
            'e' => 7,
            'r' => 8,
            'h' => 9,
            'v' => 10,
            'l' => 11,
            'u' => 12,
            'x' => 13,
            'o' => 14,
            _ => 0,
        };
        if (c == ' ')
        {
            CenterOnChosen();
        }
        else if (mode != 0)
        {
            SetMode(mode);
        }
    }

    /// <summary>Centers on the marked object, else the chosen ships' middle.</summary>
    private void CenterOnChosen()
    {
        if (Tracked is { } t)
        {
            CenterOn(t.X, t.Y);
        }
        else if (Selection.Count > 0)
        {
            var l = Selection.Min(s => s.X - (s.Radius >> 1));
            var r = Selection.Max(s => s.X + (s.Radius >> 1));
            var top = Selection.Min(s => s.Y - (s.Radius >> 1));
            var bottom = Selection.Max(s => s.Y + (s.Radius >> 1));
            CenterOn((r - l) / 2 + l, (bottom - top) / 2 + top);
        }
    }

    /// <summary>
    /// Chooses a command: retreat, fighter recall, auto launch
    /// and AUTO go off at once; the rest wait for a click on the field
    /// under cross hairs. AUTO brings back the arrow; the others leave the
    /// pointer as it was.
    /// </summary>
    public void SetMode(int mode)
    {
        Mode = mode;
        Post(ModeMessage, mode);
        if (mode == 14)
        {
            Assets.Cursor.Show(MouseCursor.Arrow);
        }

        if (mode is 7 or 8 or 13 or 14)
        {
            Give((0, 0));
        }
        else
        {
            Assets.Cursor.Show(MouseCursor.Target);
        }
    }

    /// <summary>Builds the waiting command's order at a field point and sends it.</summary>
    private void Give((int X, int Y) at)
    {
        var mode = Mode;
        Mode = 0;
        if (Battle is not { } b)
        {
            return;
        }

        // a voice answers first, even when no order results; none for AUTO.
        if (mode != 14)
        {
            PlaySound(Audio.SoundIds.BattleAccepted[Random.Shared.Next(Audio.SoundIds.BattleAccepted.Length)]);
        }

        var ids = Selection.Select(s => s.Id).ToList();
        var pick = Pick(at);
        BattleOrder? order = mode switch
        {
            1 or 2 or 9 or 10 or 11 or 12 => Order(mode, ids, -1, -1, at),
            3 or 4 when pick is not null && Owner(pick) != LocalIndex => Order(mode, ids, pick.Id, -1, at),
            5 or 6 when pick is not null && ids.Count > 0 => Order(5, ids, pick.Id, -1, (0, 0)),
            5 or 6 when pick is not null && Tracked is Orbital fighters => Order(5, [], pick.Id, fighters.Id, (0, 0)),
            7 or 8 when ids.Count > 0 => Order(mode, ids, -1, -1, (0, 0)),
            7 or 8 when Tracked is Orbital own && own.Owner == LocalIndex => Order(mode - 1, [], -1, own.Id, (0, 0)),
            13 => Order(13, ids, -1, -1, (0, 0)),
            14 when b.Ships[LocalIndex].FirstOrDefault() is { } first => Order(9, [], first.Id, -1, (0, 0)),
            _ => null,
        };
        if (order is not null)
        {
            Send?.Invoke(order);
        }

        Post(SelectionMessage, Selection.Count == 0 && Tracked is not null ? 1 : Selection.Count);
    }

    private BattleOrder Order(int kind, List<int> ids, int target, int orbital, (int X, int Y) at)
    {
        return new BattleOrder((BattleOrderKind)kind, Battle!.Id, target, orbital, at.X, at.Y, ids); // only called with a battle shown
    }
}
