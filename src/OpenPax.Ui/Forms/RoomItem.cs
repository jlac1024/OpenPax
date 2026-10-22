using OpenPax.Net.Lobby;

namespace OpenPax.Ui.Forms;

/// <summary>A room as the lobby's list shows it.</summary>
public sealed record RoomItem(RoomInfo Room)
{
    public override string ToString() =>
        $"{Room.Name}  {Room.Players}/{Room.MaxPlayers}{(Room.Locked ? UiText.RoomLockedMark : "")}{(Room.InGame ? UiText.RoomPlayingMark : "")}";
}
