using OpenPax.Data.Views;
using OpenPax.Sim.Commands;
using OpenPax.Ui.Widgets;

namespace OpenPax.Ui;

/// <summary>
/// The governor picker (popup 0x1B, VIEW 39000): the advisor for a field
/// with its skill, and every governor with theirs. PROMOTE (or a double
/// click) makes the picked governor the field's advisor, in place of the
/// one there, with no comparison and no cost; the list is then made again.
/// DONE closes it. It has no keys of its own.
/// </summary>
public sealed class GovernorPopup : IPopup
{
    public const int ViewId = 39000;
    private const int Promote = 39001;
    private const int Done = 39002;
    private const int List = 39003;
    private const int ListActivated = 39103;
    private const int AdvisorLine = 39005;

    private readonly UiApp _app;
    private readonly GameSession _session;
    private readonly int _role;

    /// <param name="role">0 research, 1 espionage, 2 domestic, 3 fleet.</param>
    public GovernorPopup(UiApp app, GameSession session, int role)
    {
        app.Assets.Cursor.Show(MouseCursor.Arrow);
        _app = app;
        _session = session;
        _role = role;

        // Open: where the original puts it on screen; the port centers it.
        var size = ViewReader.Parse(app.Assets.Views.Get(OpenPax.Data.Resources.ResourceType.View, ViewId).Span).Base;
        Window = new ViewWindow(app.Assets, ViewId, app.PaletteIndex, ViewId, null, (app.ClassicWidth - size.Width) / 2, (app.ClassicHeight - size.Height) / 2);
        Fill();
        ShowAdvisor();
    }

    public ViewWindow Window { get; }

    public bool Handle(UiMessage message)
    {
        switch (message.Message)
        {
            case Promote or ListActivated:
                // the picked row, not the one the double click names.
                if (Window.Get<ListBoxPane>(List).Selected is GovernorRow row)
                {
                    _session.Send(new AppointGovernorCommand(_session.Local.Index, row.Governor.Id, _role));

                    // The advisors changed (the player's 1036): the list and line again.
                    Fill();
                    ShowAdvisor();
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

    /// <summary>The player's governors, oldest first; the first row picked.</summary>
    private void Fill()
    {
        var list = Window.Get<ListBoxPane>(List);
        list.Clear();
        foreach (var governor in _session.Local.Advisors.Where(a => a.IsGovernor))
        {
            list.Add(new GovernorRow(governor, _session.Game.Universe.Stars[governor.Star].Name));
        }

        if (list.Rows.Count > 0)
        {
            list.Select(list.Rows[0]);
        }
    }

    /// <summary>The field's advisor and skill; left as it was when the field has none.</summary>
    private void ShowAdvisor()
    {
        if (_session.Local.Advisors.FirstOrDefault(a => a.Role == _role && a.Star == Sim.Players.Advisor.NoStar) is { } holder)
        {
            Window.Get<TextLinePane>(AdvisorLine).Text = _app.Assets.Strings.Get(new TextRef(15050, _role)) + " "
                + GovernorRow.Percent(_app.Assets, holder.Value(_role));
        }
    }
}
