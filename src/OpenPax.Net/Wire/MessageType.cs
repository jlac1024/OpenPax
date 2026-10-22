namespace OpenPax.Net.Wire;

/// <summary>The byte that names a message on the wire. Values never change once used.</summary>
public enum MessageType : byte
{
    Hello = 1,
    Welcome = 2,
    Refused = 3,
    Command = 4,
    State = 5,
    SnapshotRequest = 6,
    Chat = 7,
    SessionNews = 8,
    LobbyHello = 20,
    LobbyWelcome = 21,
    RoomListRequest = 22,
    RoomList = 23,
    CreateRoom = 24,
    JoinRoom = 25,
    LeaveRoom = 26,
    RoomState = 27,
    RoomClosed = 28,
    SetReady = 29,
    LobbyChat = 30,
    StartGame = 31,
    GameStarting = 32,
    RelayHello = 33,
    RoomSettings = 34,
    SeatChoice = 35,
    ModSet = 36,
    ModRequest = 37,
    ModChunk = 38,
}
