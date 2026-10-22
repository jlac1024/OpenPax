using OpenPax.Audio;
using OpenPax.Sim;
using OpenPax.Sim.Commands;
using OpenPax.Ui.Widgets;

namespace OpenPax.Ui.Screens;

/// <summary>
/// The developers' cheat window (mode 21, VIEW 27000, palette 5): the
/// players in a list and a button per cheat for the one picked. The shipped
/// game cannot reach it; the port opens it from its console's "devwindow"
/// once cheats are on (docs/ui/developer-window.md).
/// </summary>
public sealed class DeveloperScreen : Screen
{
    public const int ViewId = 27000;
    public const int PaletteIndex = 5;

    private const int PlayerList = 27001;
    private const int FirstButton = 27003;
    private const int LastButton = 27013;

    private readonly GameSession _session;
    private readonly List<(CaptionRow Row, int Player)> _rows = [];

    public DeveloperScreen(UiApp app)
        : base(app, new ViewWindow(app.Assets, ViewId, PaletteIndex, ViewId))
    {
        app.Assets.Cursor.Show(MouseCursor.Arrow);
        _session = app.Session ?? throw new InvalidOperationException("The cheat window needs a game.");
        var list = Window.Get<ListBoxPane>(PlayerList);
        foreach (var p in _session.Game.Players)
        {
            var row = new CaptionRow(p.Name, 9, 255);
            _rows.Add((row, p.Index));
            list.Add(row);
        }

        list.Select(_rows.FirstOrDefault().Row);

        app.Sound.Play(SoundIds.What);
        _ = Window.TakeMessages().ToList();
    }

    public override void Handle(UiMessage message, long now)
    {
        if (message.Message is < FirstButton or > LastButton)
        {
            return;
        }

        // to: the cheat for the player picked.
        var picked = Window.Get<ListBoxPane>(PlayerList).Selected;
        if (_rows.FirstOrDefault(r => r.Row == picked) is { Row: not null } entry)
        {
            App.GameEvents(_session.Send(new CheatCommand(entry.Player, (Cheat)(message.Message - FirstButton))));
        }
    }
}
