using OpenPax.Sim;

namespace OpenPax.Net.State;

/// <summary>A notice as data, players by index and objects by id, so it can travel to a client and be found again in its game.</summary>
public sealed record NoticeData(NoticeKind Kind, int Recipient, int Value, int Other, int Place, int Ship, int Count, bool Named)
{
    /// <summary>A mod's message's words (NoticeKind.ModMessage), else null.</summary>
    public string? Text { get; init; }

    public static NoticeData Of(GameNotice notice, Game game) => new(
        notice.Kind,
        notice.Recipient.Index,
        notice.Value,
        notice.Other?.Index ?? -1,
        game.ObjectIdOf(notice.Place),
        notice.Ship?.ShipId ?? -1,
        notice.Count,
        notice.Named)
    {
        Text = notice.Text,
    };

    /// <summary>The notice again in the client's game; a place or ship it no longer has is left out.</summary>
    public GameNotice ToNotice(Game game) => new(
        Kind,
        game.Players[Recipient],
        Value,
        Other >= 0 && Other < game.Players.Count ? game.Players[Other] : null,
        Place >= 0 ? game.SpaceObject(Place) : null,
        Ship >= 0 ? game.FindShip(Ship) : null,
        Count,
        Named,
        Text);
}
