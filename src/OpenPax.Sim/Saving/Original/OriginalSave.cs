using OpenPax.Sim.Setup;

namespace OpenPax.Sim.Saving.Original;

/// <summary>
/// The original game's saved games (docs/formats/saves.md): single player
/// saves of version 1.072 can be loaded. The original keeps no random state
/// and draws while loading; the port starts the stream again from the
/// saved seed and makes the same draws, so a file always loads the same way.
/// </summary>
public static class OriginalSave
{
    /// <summary>"mGxP" as a little endian number: a single player save.</summary>
    public const uint SinglePlayerMagic = 0x5078476D;

    /// <summary>1.072, the only version the original accepts.</summary>
    public const int Version = 0x430;

    /// <summary>Whether a file is an original single player save the port can read.</summary>
    public static bool IsSave(Stream stream)
    {
        try
        {
            return new OriginalSaveReader(stream, null!, null).ReadHeader(); // only the header is read; the tables are never touched
        }
        catch (EndOfStreamException)
        {
            return false;
        }
    }

    /// <summary>
    /// Reads a save. The game comes with the star the original's view showed
    /// and the id of the object it had picked (-1 for none).
    /// </summary>
    public static (Game Game, int CurrentStar, int SelectionId) Load(Stream stream, GameTables tables, IComputerPlayer? computerPlayers)
    {
        var reader = new OriginalSaveReader(stream, tables, computerPlayers);
        if (!reader.ReadHeader())
        {
            throw new InvalidDataException(Strings.OriginalSaveNot);
        }

        var game = reader.ReadGame();
        game.Tick = (((long)(game.Calendar.Year - Time.Calendar.FirstYear) * Time.Calendar.StepsPerYear) + game.Calendar.Step) * 4;
        return (game, reader.CurrentStar, reader.SelectionId);
    }
}
