namespace OpenPax.Ui;

public enum HostRequestKind
{
    Quit,
    PlayMovie,
    StartGame,

    /// <summary>The hosting player's network game: hand it back through <see cref="UiApp.HostedGameReady"/>.</summary>
    StartNetworkGame,

    /// <summary>Ask GitHub for a newer release now (Options); the answer goes to <see cref="UiApp.Updates"/>.</summary>
    CheckForUpdates,

    /// <summary>Download and install <see cref="UpdateState.Available"/>, then restart.</summary>
    Update,

    /// <summary>Stop a download under way.</summary>
    CancelUpdate,
}
