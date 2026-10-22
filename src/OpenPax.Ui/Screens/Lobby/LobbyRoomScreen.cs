using OpenPax.Ui.Forms;

namespace OpenPax.Ui.Screens.Lobby;

/// <summary>The port's own (mode 4): a room before its game, as a form (Forms/Markup/LobbyRoom.xml).</summary>
public sealed class LobbyRoomScreen : FormScreen
{
    private readonly LobbyRoomModel _model;

    public LobbyRoomScreen(UiApp app)
        : this(app, new LobbyRoomModel(app))
    {
    }

    private LobbyRoomScreen(UiApp app, LobbyRoomModel model)
        : base(app, GameForms.Read("LobbyRoom", model))
    {
        _model = model;
        Form.Bind(model);
    }

    public override void Tick(long now) => _model.Tick(now);

    public override void Closed()
    {
        base.Closed();
        _model.Closed();
    }
}
