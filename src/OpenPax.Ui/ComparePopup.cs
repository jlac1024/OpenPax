using OpenPax.Data.Views;
using OpenPax.Sim.Players;
using OpenPax.Ui.Widgets;

namespace OpenPax.Ui;

/// <summary>
/// Compare Empires (popup 0x1E, VIEW 42000): the empire history graph. A
/// toggle per player, in index order, turns its line on or off; the arrows
/// step through the ten statistics; pointing at a toggle names its player.
/// Every line starts on, on the first statistic, the treasury.
/// </summary>
public sealed class ComparePopup : IPopup
{
    public const int ViewId = 42000;
    private const int Done = 42001;
    private const int Back = 42002;
    private const int Forward = 42003;
    private const int FirstToggle = 42004;
    private const int Toggles = 16;
    private const int Graph = 42020;
    private const int StatisticName = 42021;
    private const int HoverName = 42022;

    private readonly UiApp _app;
    private readonly GameSession _session;

    public ComparePopup(UiApp app, GameSession session)
    {
        app.Assets.Cursor.Show(MouseCursor.Arrow);
        _app = app;
        _session = session;

        // Open: where the original puts it on screen; the port centers it.
        var size = ViewReader.Parse(app.Assets.Views.Get(OpenPax.Data.Resources.ResourceType.View, ViewId).Span).Base;
        Window = new ViewWindow(app.Assets, ViewId, app.PaletteIndex, ViewId, MakePanel, (app.ClassicWidth - size.Width) / 2, (app.ClassicHeight - size.Height) / 2);
        GraphPane.Game = session.Game;

        // the toggles go to the players in order, each on
        // with its line drawn; the toggles left over stay empty and disabled.
        for (var t = 0; t < Toggles; t++)
        {
            var toggle = Window.Get<EmpireTogglePane>(FirstToggle + t);
            toggle.Player = null;
            toggle.Set(false);
            toggle.Enabled = false;
        }

        var next = FirstToggle;
        foreach (var player in session.Game.Players)
        {
            var toggle = Window.Get<EmpireTogglePane>(next++);
            (toggle.Player, toggle.Viewer) = (player, session.Local);
            toggle.Enabled = true;
            toggle.Set(true);
            GraphPane.Show(player.Index, true);
        }

        ShowStatisticName();
    }

    public ViewWindow Window { get; }

    private HistoryGraphPane GraphPane => Window.Get<HistoryGraphPane>(Graph);

    private static Pane? MakePanel(ViewNode node, int left, int top)
    {
        return node.ClassId == 8040 ? new HistoryGraphPane(node, left, top) : null;
    }

    public bool Handle(UiMessage message)
    {
        var id = message.Message;
        switch (id)
        {
            case Done:
                return true;
            case Back or Forward:
                GraphPane.Step(id == Forward ? 1 : -1);
                ShowStatisticName();
                return false;
            case >= FirstToggle and < FirstToggle + Toggles:
                // by toggle number, which is the player index.
                GraphPane.Show(id - FirstToggle, message.Value != 0);
                return false;
            case UiMessage.Within:
                ShowName(message.Value);
                return false;
            case UiMessage.Left:
                ShowName(0);
                return false;
            default:
                return false;
        }
    }

    public bool Key(UiKey key)
    {
        return false;
    }

    // STR# 15025 by statistic.
    private void ShowStatisticName()
    {
        SetText(StatisticName, _app.Assets.Strings.Get(new TextRef(15025, GraphPane.Statistic - 1)), null);
    }

    /// <summary>The pointed toggle's player name in the relation color, else nothing.</summary>
    private void ShowName(int toggleId)
    {
        if (toggleId is >= FirstToggle and < FirstToggle + Toggles && Window.Get<EmpireTogglePane>(toggleId).Player is { } player)
        {
            var level = player == _session.Local ? 4 : _session.Local.Treaties[player.Index].Level();
            byte color = level switch
            {
                Treaty.None => 0xFF,
                < 1 => 0x12,
                <= 3 => 0x10,
                _ => 0x16,
            };
            SetText(HoverName, player.Name, color);
            return;
        }

        SetText(HoverName, "", null);
    }

    private void SetText(int id, string text, byte? color)
    {
        switch (Window.Get(id))
        {
            case LabelPane label:
                label.Text = text;
                label.Color = color ?? label.Color;
                break;
            case TextLinePane line:
                line.Text = text;
                line.Color = color ?? line.Color;
                break;
            default:
                break;
        }
    }
}
