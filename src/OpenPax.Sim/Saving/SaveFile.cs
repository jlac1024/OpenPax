using System.Reflection;
using OpenPax.Sim.Setup;

namespace OpenPax.Sim.Saving;

/// <summary>
/// The port's saved games (docs/formats/port-saves.md): "HGSV", a version,
/// then the saved object graph, whose root is whatever the caller saves (the
/// game, with the screens' own state around it). The whole game is kept,
/// the random number state included, so a loaded game goes on tick for tick
/// as if it had never stopped.
/// </summary>
public static class SaveFile
{
    /// <summary>"HGSV" read as a little endian number.</summary>
    public const uint Magic = 0x56534748;

    /// <summary>2: the header names the game's mods, so a save list can show them without reading the game.</summary>
    public const int Version = 2;

    /// <summary>Whether a file starts like a port save.</summary>
    public static bool IsSave(Stream stream)
    {
        Span<byte> head = stackalloc byte[4];
        return stream.ReadAtLeast(head, 4, throwOnEndOfStream: false) == 4 && BitConverter.ToUInt32(head) == Magic;
    }

    /// <summary>Writes a save: the header with the game's mods ("id@version"), then the root.</summary>
    public static void Write(Stream stream, object root, GameTables tables, IReadOnlyList<string>? mods = null)
    {
        var writer = new SaveWriter(stream, tables);
        writer.WriteHeader(Version, mods ?? []);
        writer.Value(root);
        writer.Flush();
    }

    /// <summary>
    /// The mods a save's header names, read without the game; null for a
    /// version 1 save (made before the header had them) or a file that is
    /// not a save.
    /// </summary>
    public static IReadOnlyList<string>? ReadMods(Stream stream)
    {
        try
        {
            using var reader = new BinaryReader(stream, System.Text.Encoding.UTF8, leaveOpen: true);
            if (reader.ReadUInt32() != Magic || reader.ReadInt32() is var version && version < 2)
            {
                return null;
            }

            var count = reader.ReadInt32();
            if (count is < 0 or > 1000)
            {
                return null;
            }

            var mods = new List<string>(count);
            for (var i = 0; i < count; i++)
            {
                mods.Add(reader.ReadString());
            }

            return mods;
        }
        catch (Exception e) when (e is EndOfStreamException or IOException)
        {
            return null;
        }
    }

    /// <summary>
    /// Reads a save's root. The tables must be the game's own data; the
    /// assemblies are where types other than the simulation's live.
    /// </summary>
    public static object? Read(Stream stream, GameTables tables, IComputerPlayer? computerPlayers, params Assembly[] assemblies)
    {
        var reader = new SaveReader(stream, tables, computerPlayers, assemblies);
        var version = reader.ReadHeader();
        if (version > Version)
        {
            throw new InvalidDataException(string.Format(Strings.SaveTooNew, version));
        }

        return reader.Value();
    }
}
