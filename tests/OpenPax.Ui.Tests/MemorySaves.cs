using OpenPax.Sim.Saving;

namespace OpenPax.Ui.Tests;

/// <summary>Saved games kept in memory, written and read in the real format.</summary>
internal sealed class MemorySaves : ISaveGames
{
    public Dictionary<string, byte[]> Files { get; } = [];

    public IReadOnlyList<string> Names() => [.. Files.Keys.Order(StringComparer.Ordinal)];

    public bool Save(string name, SavedSession session)
    {
        using var stream = new MemoryStream();
        SaveFile.Write(stream, session, session.Game.Tables);
        Files[name] = stream.ToArray();
        return true;
    }

    public SavedSession? Load(string name)
    {
        if (!Files.TryGetValue(name, out var bytes))
        {
            return null;
        }

        using var stream = new MemoryStream(bytes);
        return SaveFile.Read(stream, UiGames.Tables(), null, typeof(SavedSession).Assembly) as SavedSession;
    }

    public void Delete(string name) => Files.Remove(name);
}
