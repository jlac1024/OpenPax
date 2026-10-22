using OpenPax.Data.Views;
using OpenPax.Sim.Commands;
using OpenPax.Sim.Players;
using OpenPax.Ui.Widgets;

namespace OpenPax.Ui;

/// <summary>
/// The upgrade popup (0x19, VIEW 20700): the selected fleet's ships not yet
/// marked for refit, and for the picked ship the newer designs of its hull
/// size. UPGRADE marks the ship; it is refitted when the shipyard its fleet
/// circles has nothing else to build. The popup stays open until DONE.
/// </summary>
public sealed class UpgradePopup : IPopup
{
    public const int ViewId = 20700;
    private const int Upgrade = 20701;
    private const int Done = 20702;
    private const int ShipList = 20703;
    private const int DesignList = 20704;
    private const int Title = 20707;

    private readonly GameSession _session;

    public UpgradePopup(UiApp app, GameSession session)
    {
        _session = session;

        // Open: where the original puts it on screen; the port centers it.
        var size = ViewReader.Parse(app.Assets.Views.Get(OpenPax.Data.Resources.ResourceType.View, ViewId).Span).Base;
        Window = new ViewWindow(app.Assets, ViewId, app.PaletteIndex, ViewId, null, (app.ClassicWidth - size.Width) / 2, (app.ClassicHeight - size.Height) / 2);
        FillShips();
        Window.Get<TextLinePane>(Title).Text = (session.Selection as Fleet)?.Name ?? "";
    }

    public ViewWindow Window { get; }

    public bool Handle(UiMessage message)
    {
        switch (message.Message)
        {
            case ShipList:
                FillDesigns();
                return false;
            case Upgrade:
                // the marked ship then leaves the list.
                if (Window.Get<ListBoxPane>(ShipList).Selected is ShipRow ship && Window.Get<ListBoxPane>(DesignList).Selected is RefitRow design)
                {
                    _session.Send(new RefitCommand(_session.Local.Index, ship.Ship.ShipId, design.Design.Slot));
                    FillShips();
                }

                return false;
            case Done:
                return true;
            default:
                return false;
        }
    }

    public bool Key(UiKey key)
    {
        return false;
    }

    /// <summary>
    /// The ships, oldest at the top, if the selection is a fleet
    /// with no orders circling a shipyard's planet; the first is picked and its
    /// designs listed. Otherwise the ship list is empty and the designs stay.
    /// </summary>
    private void FillShips()
    {
        var list = Window.Get<ListBoxPane>(ShipList);
        list.Clear();
        if (_session.Selection is not Fleet { Orders.Count: 0 } fleet || !Sim.Game.AtShipyard(fleet))
        {
            return;
        }

        for (var i = fleet.Ships.Count - 1; i >= 0; i--)
        {
            if (fleet.Ships[i].Refit is null)
            {
                list.Add(new ShipRow(fleet.Ships[i]));
            }
        }

        // With every ship marked the picking clears the designs too.
        if (list.Rows.Count > 0)
        {
            list.Select(list.Rows[0]);
        }

        FillDesigns();
    }

    /// <summary>The designs the picked ship may become; the first is picked.</summary>
    private void FillDesigns()
    {
        var list = Window.Get<ListBoxPane>(DesignList);
        list.Clear();
        if (Window.Get<ListBoxPane>(ShipList).Selected is not ShipRow row)
        {
            return;
        }

        foreach (var design in Sim.Game.RefitChoices(_session.Local, row.Ship))
        {
            list.Add(new RefitRow(design));
        }

        if (list.Rows.Count > 0)
        {
            list.Select(list.Rows[0]);
        }
    }
}
