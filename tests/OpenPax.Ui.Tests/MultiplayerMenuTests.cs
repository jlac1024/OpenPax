using OpenPax.Ui.Screens;
using OpenPax.Ui.Widgets;
using static OpenPax.Ui.Tests.UiGames;

namespace OpenPax.Ui.Tests;

/// <summary>Game Start's multiplayer menu offers PUBLIC LOBBY only when config.json names one.</summary>
public sealed class MultiplayerMenuTests
{
    [Fact]
    public void WithNoPublicLobbyTheOthersMoveUp()
    {
        var app = NewApp();
        var window = OpenMenu(app);
        Assert.Equal(UiText.MenuLan, window.Get<ButtonPane>(31100).Caption);
        Assert.Equal(UiText.MenuPrivateLobby, window.Get<ButtonPane>(31101).Caption);
        Assert.Equal(UiText.MenuPreviousMenu, window.Get<ButtonPane>(31102).Caption);
        Assert.False(window.Get(31103).Visible);

        // The private lobby's page opens from the second place.
        var b = window.Get(31101);
        Click(app, b.Left + 5, b.Top + 5);
        Assert.Equal(ScreenMode.LobbyConnect, app.Mode);
        Assert.Equal(Forms.LobbyChoice.Private, app.LobbyChoice);
    }

    [Fact]
    public void ANamedPublicLobbyIsOffered()
    {
        var app = NewApp();
        app.PublicLobbyAddress = "lobby.example.com";
        var window = OpenMenu(app);
        Assert.Equal(UiText.MenuPublicLobby, window.Get<ButtonPane>(31101).Caption);
        Assert.Equal(UiText.MenuPreviousMenu, window.Get<ButtonPane>(31103).Caption);

        // Previous Menu brings every main button back.
        var back = window.Get(31103);
        Click(app, back.Left + 5, back.Top + 5);
        Assert.True(window.Get(31103).Visible);
        Assert.Null(window.Get<ButtonPane>(31101).Caption);
    }

    private static ViewWindow OpenMenu(UiApp app)
    {
        var multiplayer = app.Current.Window.Get(31101);
        Click(app, multiplayer.Left + 5, multiplayer.Top + 5);
        return app.Current.Window;
    }
}
