using OpenPax.Data.Views;
using OpenPax.Render;
using OpenPax.Sim.Combat;
using OpenPax.Ui.Widgets;

namespace OpenPax.Ui.Battle;

/// <summary>
/// The battle's minimap (class 24002): every ship and orbital
/// as a dot in its relation color, fighters out as single pixels, the
/// planet, and the box the field shows. Dragging with the left button
/// scrolls the field; the right button moves the chosen ships there.
/// </summary>
public sealed class BattleMinimapPane(ViewNode node, int left, int top) : Pane(node, left, top)
{
    private const double Up = 1.743446795;

    public GameSession? Session { get; set; }

    public BattleFieldPane? Field { get; set; }

    /// <summary>A field pixel on the minimap.</summary>
    private (int X, int Y) ToMap(int fx, int fy) => (150 * fx / BattleFieldPane.FieldWidth + Left, 87 * fy / BattleFieldPane.FieldHeight + Top);

    private (int X, int Y) ToMapWorld(int x, int y) => ToMap(x, (int)(y * 0.573576436351046 + 0.5));

    protected override void Draw(Framebuffer target)
    {
        var main = Assets.Pictures(ImageSet.Main);
        if (main.Get(24002) is { } back)
        {
            target.Draw(back, Left, Top);
        }

        if (Field?.Battle is { } b && Session is { } session)
        {
            var dot = Assets.Pictures(ImageSet.Combat).Get(BattleArt.MinimapDot);
            for (var p = 0; p < Sim.Combat.Battle.Players; p++)
            {
                var color = BattleArt.Relation(session.Game.Players, session.Local.Index, p);
                foreach (var o in b.Ships[p].Where(s => !s.Dead).Cast<BattleObject>().Concat(b.Orbitals[p]))
                {
                    var (x, y) = ToMapWorld(o.X, o.Y);
                    if (dot is not null)
                    {
                        target.DrawTinted(dot, x - 1, y - 1, color);
                    }
                }

                foreach (var f in b.Fighters[p].Where(f => !f.Docked))
                {
                    var (x, y) = ToMapWorld(f.X, f.Y);
                    target.FillRect(x, y, 1, 1, color);
                }
            }

            if (b.PlanetObject is not null && Assets.Pictures(ImageSet.Combat).Get(BattleArt.MinimapPlanet) is { } planet)
            {
                var (x, y) = ToMapWorld(3280, 3240);
                target.Draw(planet, x, y);
            }

            // The box: 25 wide, and 14 or 17 high by the field's height (quirk kept: 334, not 335).
            var (bx, by) = ToMap(Field.ScrollX, Field.ScrollY);
            var h = 87 * (Field.Height == BattleFieldPane.FullHeight ? 423 : 334) / BattleFieldPane.FieldHeight;
            var w = 150 * 600 / BattleFieldPane.FieldWidth;
            target.DrawLine(bx, by, bx + w - 1, by, 18);
            target.DrawLine(bx, by + h - 1, bx + w - 1, by + h - 1, 18);
            target.DrawLine(bx, by, bx, by + h - 1, 18);
            target.DrawLine(bx + w - 1, by, bx + w - 1, by + h - 1, 18);
        }

        if (main.Get(24004) is { } corner)
        {
            target.Draw(corner, Left, Top + Height - corner.Height);
        }
    }

    /// <summary>The right button moves the chosen ships to the point.</summary>
    protected internal override void MouseDown(int x, int y, int clicks)
    {
        if (Window?.RightButton != true || Field is not { Battle: { } b } field || field.Selection.Count == 0)
        {
            return;
        }

        PlaySound(Audio.SoundIds.BattleAccepted[Random.Shared.Next(Audio.SoundIds.BattleAccepted.Length)]);
        var fx = BattleFieldPane.FieldWidth * (x - Left) / 150;
        var fy = (int)(BattleFieldPane.FieldHeight * (y - Top) / 87 * Up + 0.5);
        field.Send?.Invoke(new BattleOrder(BattleOrderKind.Move, b.Id, -1, -1, fx, fy, [.. field.Selection.Select(s => s.Id)]));
    }

    /// <summary>Dragging with the left button scrolls the field to center the box there.</summary>
    protected internal override void MouseMove(int x, int y, long now)
    {
        if (Window?.RightButton == true || Field is not { } field)
        {
            return;
        }

        var w = 150 * 600 / BattleFieldPane.FieldWidth;
        var h = 87 * (field.Height == BattleFieldPane.FullHeight ? 423 : 334) / BattleFieldPane.FieldHeight;
        var mx = x - Left - w / 2;
        var my = y - Top - h / 2;
        field.ScrollTo(BattleFieldPane.FieldWidth * mx / 150, BattleFieldPane.FieldHeight * my / 87);
    }
}
