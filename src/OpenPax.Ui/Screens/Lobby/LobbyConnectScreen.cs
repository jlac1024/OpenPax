using OpenPax.Ui.Forms;

namespace OpenPax.Ui.Screens.Lobby;

/// <summary>The port's own (mode 2): the MULTIPLAYER menu and connecting to a lobby, as a form (Forms/Markup/LobbyConnect.xml).</summary>
public sealed class LobbyConnectScreen : FormScreen
{
    private readonly LobbyConnectModel _model;

    public LobbyConnectScreen(UiApp app)
        : this(app, new LobbyConnectModel(app))
    {
    }

    private LobbyConnectScreen(UiApp app, LobbyConnectModel model)
        : base(app, GameForms.Read("LobbyConnect", model))
    {
        _model = model;
        app.LobbyStatus = null;
        Form.Bind(model);
    }

    public override void Tick(long now) => _model.Tick();

    public override void Closed()
    {
        _model.Dispose();
        base.Closed();
    }
}
