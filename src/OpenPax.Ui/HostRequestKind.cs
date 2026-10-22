namespace OpenPax.Ui;

public enum HostRequestKind
{
    Quit,
    PlayMovie,
    StartGame,

    /// <summary>The hosting player's network game: hand it back through <see cref="UiApp.HostedGameReady"/>.</summary>
    StartNetworkGame,
}
