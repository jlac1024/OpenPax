namespace OpenPax.Ui;

/// <summary>
/// Saved games as the screens see them (the "Saved Games" folder): the host
/// gives the files and the format.
/// </summary>
public interface ISaveGames
{
    /// <summary>The saves the game can load, by name, in listing order.</summary>
    IReadOnlyList<string> Names();

    /// <summary>Writes the game under the name, replacing a save of that name; false when it could not.</summary>
    bool Save(string name, SavedSession session);

    /// <summary>The game saved under the name, or null when it cannot be read.</summary>
    SavedSession? Load(string name);

    void Delete(string name);

    /// <summary>The mods ("id@version") the save was played with; empty for none, null when that is not known.</summary>
    IReadOnlyList<string>? ModsOf(string name) => null;
}
