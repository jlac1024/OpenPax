using OpenPax.Ui.Forms;

namespace OpenPax.Ui.Screens.Lobby;

/// <summary>The port's own (mode 3): the lobby's rooms and chat, as a form (Forms/Markup/LobbyRooms.xml).</summary>
public sealed class LobbyRoomsScreen : FormScreen
{
    private readonly LobbyRoomsModel _model;

    public LobbyRoomsScreen(UiApp app)
        : this(app, new LobbyRoomsModel(app))
    {
    }

    private LobbyRoomsScreen(UiApp app, LobbyRoomsModel model)
        : base(app, GameForms.Read("LobbyRooms", model))
    {
        _model = model;
        Form.Bind(model);
    }

    public override void Tick(long now) => _model.Tick();
}
