namespace OpenPax.Ui.Forms;

/// <summary>Game Start's three multiplayer choices; None is none made.</summary>
public enum LobbyChoice
{
    None,

    /// <summary>A lobby found on the LAN by broadcast.</summary>
    Lan,

    /// <summary>The public lobby named in config.json beside the game.</summary>
    Public,

    /// <summary>A lobby whose address or DNS name the player types.</summary>
    Private,
}
