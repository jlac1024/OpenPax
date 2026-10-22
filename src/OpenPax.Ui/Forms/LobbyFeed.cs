using System.Collections.ObjectModel;
using OpenPax.Net.Lobby;
using OpenPax.Ui.Screens;

namespace OpenPax.Ui.Forms;

/// <summary>What the lobby forms share: the connection's messages each frame, the chat, and a lost connection going back to connect.</summary>
internal static class LobbyFeed
{
    private const int MaxChat = 200;

    /// <summary>The chat heard so far, for a form's list.</summary>
    public static ObservableCollection<string> Chat(UiApp app) => [.. app.LobbyChat];

    /// <summary>Pumps the lobby; null when the connection is gone (the connect screen is then up), else whether anything changed.</summary>
    public static bool? Pump(UiApp app, ObservableCollection<string> chat)
    {
        if (app.Lobby is not { Connected: true } lobby)
        {
            app.Lobby?.Dispose();
            app.Lobby = null;
            app.LobbyStatus = UiText.LobbyLost;
            app.Switch(ScreenMode.LobbyConnect);
            return null;
        }

        if (!lobby.Pump())
        {
            return false;
        }

        foreach (var line in lobby.TakeChat())
        {
            var text = line.From + ": " + line.Text;
            app.LobbyChat.Add(text);
            chat.Add(text);
        }

        if (app.LobbyChat.Count > MaxChat)
        {
            app.LobbyChat.RemoveRange(0, app.LobbyChat.Count - MaxChat);
        }

        while (chat.Count > MaxChat)
        {
            chat.RemoveAt(0);
        }

        return true;
    }

    public static void Say(LobbyClient lobby, string text)
    {
        if (text.Trim().Length > 0)
        {
            lobby.Say(text);
        }
    }
}
