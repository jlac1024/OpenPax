using OpenPax.Data.Views;
using OpenPax.Render;
using OpenPax.Sim;
using OpenPax.Sim.Galaxy;
using OpenPax.Sim.Numerics;

namespace OpenPax.Ui.Widgets;

/// <summary>
/// Class 14001, the planet's surface: the landscape for its class (changed
/// by population improvements), a building stack for each resource's
/// finished improvements, the one being built with a crane, crowds by
/// population and eight ambient loops. A planet without a colony in view
/// shows bare. Animations step every 66 ms of screen time.
/// </summary>
public sealed class SurfacePane(ViewNode node, int left, int top) : Pane(node, left, top)
{
    private const int FrameTime = 66;

    // Ambient loops: increments of their restart limits, frame counts and
    // the population above which each plays.
    private static readonly int[] _increments = [30, 23, 16, 20, 28, 21, 10, 18];
    private static readonly int[] _loopFrames = [60, 60, 60, 60, 60, 30, 15, 60];
    private static readonly int[] _loopPopulation = [0, 18, 36, 36, 54, 54, 72, 72];

    // Building stacks by resource: first picture past the class base when
    // built only, and where it sits; the crane's site is the stack's top.
    private static readonly int[] _stackPicture = [249, 549, 99, 399];
    private static readonly (int X, int Y)[] _stackAt = [(354, 61), (0, 37), (158, 39), (484, 26)];
    private static readonly (int X, int Y)[] _siteAt = [(354, 311), (0, 310), (158, 310), (484, 311)];

    // The "work starts" animation's offset by resource.
    private static readonly int[] _workOffset = [200, 0, 600, 400];

    private readonly int[] _counters = new int[8];
    private readonly int[] _limits = new int[8];
    private long _lastStep = long.MinValue;
    private int _populationFrame;
    private int _workFrame;
    private bool _working;
    private bool _crane;
    private bool _craneUp;
    private int _craneFrame;
    private int _building = -1;
    private Planet? _planet;

    public GameSession? Session { get; set; }

    /// <summary>The PlaySurfaceAnim preference (+0xB5): off, only the landscape and buildings draw.</summary>
    public bool Animate { get; set; } = true;

    public Planet? Planet
    {
        get => _planet;
        set
        {
            _planet = value;
            Reset();
        }
    }

    private void Reset()
    {
        _populationFrame = 0;
        (_working, _workFrame) = (false, 0);
        (_crane, _craneFrame) = (false, 0);
        _building = -1;
        int[] counters = [90, 110, 135, 150, 170, 135, 120, 250];
        int[] limits = [250, 120, 135, 170, 150, 135, 110, 90];
        counters.CopyTo(_counters, 0);
        limits.CopyTo(_limits, 0);
        _craneUp = true;
    }

    /// <summary>One step every 66 ms; the counters run even with animations off.</summary>
    protected internal override void Tick(long now)
    {
        if (_lastStep == long.MinValue)
        {
            _lastStep = now;
        }

        if (now - _lastStep < FrameTime)
        {
            return;
        }

        _lastStep = now;
        _populationFrame = _populationFrame >= 119 ? 0 : _populationFrame + 1;
        if (_working && ++_workFrame > 89)
        {
            (_working, _workFrame) = (false, 0);
        }

        for (var k = 0; k < _counters.Length; k++)
        {
            if (++_counters[k] >= _limits[k])
            {
                _counters[k] = 0;
                _limits[k] += _increments[k];
                if (_limits[k] > 300)
                {
                    _limits[k] = 100;
                }
            }
        }

        if (_crane)
        {
            if (_craneUp)
            {
                if (++_craneFrame > 178)
                {
                    _craneUp = false;
                }
            }
            else if (--_craneFrame < 1)
            {
                _craneUp = true;
                _craneFrame = Math.Max(_craneFrame, 0);
            }
        }
    }

    protected override void Draw(Framebuffer target)
    {
        if (_planet is not { } planet || Session is not { } session)
        {
            return;
        }

        var surface = Assets.Pictures(ImageSet.Surface);
        var b = (planet.Class + 1) * 1000;

        // A pane grown wider than the landscape (docs/ui/widescreen.md) keeps
        // the landscape and all on it centered, its own size.
        var middle = Grown / 2;
        void At(int picture, int x, int y)
        {
            if (surface.Get(picture) is { } sprite)
            {
                // Positions are the window's; the pane starts 20 down.
                target.Draw(sprite, Left + middle + x, Top - 20 + y);
            }
        }

        // The landscape, and beside it on each side plain black with a thin
        // edge (a reflection of the landscape was tried, its mountains read as
        // mirrored; then a starfield). Drawn in the nearest colors of the
        // planet's palette, which changes with the planet.
        void Landscape(int picture)
        {
            At(picture, 0, 20);
            if (middle > 0 && surface.Get(picture) is { } sprite)
            {
                var black = target.Nearest(0, 0, 0);
                target.FillRect(Left, Top, middle, Height, black);
                target.FillRect(Left + middle + sprite.Width, Top, Math.Max(0, Width - middle - sprite.Width), Height, black);
                var edge = target.Nearest(110, 115, 125);
                target.FillRect(Left + middle - 1, Top, 1, Height, edge);
                target.FillRect(Left + middle + sprite.Width, Top, 1, Height, edge);
            }
        }

        var star = session.Game.Universe.Stars.First(s => s.Planets.Contains(planet));
        if (planet.Colony is not { } colony || star.Sight[session.Local.Index] != Star.InView)
        {
            Landscape(b - 50);
            return;
        }

        var n = Game.FinishedByResource(colony);
        var next = FirstUnfinished(session.Game, colony);
        var g = next?.Tech.Resource ?? -1;
        var (siteX, siteY) = g is >= 0 and < 4 ? (_siteAt[g].X, _siteAt[g].Y - (27 * n[g])) : (-1, -1);
        var nextId = next?.Tech.Id ?? -1;
        if (_building != nextId)
        {
            _building = nextId;
            (_crane, _craneFrame) = (false, 0);
            if (next is not null && g != 4)
            {
                (_working, _workFrame) = (true, 0);
            }
        }

        Landscape(n[4] < 1 ? b - 40 : b + 599 + n[4]);
        var population = X87.ToInt(colony.CurrentPopulation);
        var offset = g is >= 0 and < 4 ? _workOffset[g] : 0;
        if (Animate)
        {
            if (population > 0)
            {
                var tier = population <= 18 ? 0 : population <= 36 ? 1 : population <= 54 ? 2 : population <= 72 ? 3 : 4;
                At(20000 + (tier * 1000) + _populationFrame, 0, 20);
            }

            if (!_crane && _workFrame > 44)
            {
                (_crane, _craneFrame) = (true, 0);
            }

            if (_crane && siteX >= 0 && siteY >= 0)
            {
                At(30200 + _craneFrame, siteX - 9, siteY - 70);
            }

            if (_working)
            {
                // Quirk kept: with no site the animation sits at the top with no offset.
                At(7900 + _workFrame + offset, 0, siteY < 0 ? siteY : siteY - 185);
            }
        }

        for (var r = 0; r < 4; r++)
        {
            var building = r == g;
            if (n[r] > 0 || building)
            {
                At(b + _stackPicture[r] + n[r] + (building ? -99 : 0), _stackAt[r].X, _stackAt[r].Y);
            }
        }

        if (!Animate)
        {
            return;
        }

        if (_crane && siteX >= 0 && siteY >= 0)
        {
            At(30000 + _craneFrame, siteX - 40, siteY - 50);
        }

        if (_working)
        {
            At(7800 + _workFrame + offset, 0, siteY < 0 ? siteY : siteY - 165);
        }

        for (var k = 0; k < _counters.Length; k++)
        {
            if (_counters[k] < _loopFrames[k] && population > _loopPopulation[k])
            {
                At(7000 + (k * 100) + _counters[k], 0, 20);
            }
        }
    }

    private static Sim.Players.Improvement? FirstUnfinished(Game game, Sim.Players.Colony colony)
    {
        var list = game.UnfinishedImprovements(colony);
        return list.Count > 0 ? list[0] : null;
    }
}
