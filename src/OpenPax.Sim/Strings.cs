namespace OpenPax.Sim;

/// <summary>Messages the simulation can give, in one place.</summary>
internal static class Strings
{
    public const string SaveNotOurs = "Not a OpenPax saved game.";
    public const string SaveTooNew = "The saved game is from a newer version ({0}).";
    public const string SaveBadTag = "The saved game is damaged (unexpected value {0}).";
    public const string SaveBadTable = "The saved game refers to game data it does not know ({0}).";
    public const string SaveUnknownType = "The saved game holds a kind of object this version does not know ({0}).";
    public const string OriginalSaveNot = "Not a single player saved game of Pax Imperia: Eminent Domain 1.072.";
    public const string SaveCannotWrite = "This kind of object cannot be saved: {0}.";
}
