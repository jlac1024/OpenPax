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

    public const int Version = 1;

    /// <summary>Whether a file starts like a port save.</summary>
    public static bool IsSave(Stream stream)
    {
        Span<byte> head = stackalloc byte[4];
        return stream.ReadAtLeast(head, 4, throwOnEndOfStream: false) == 4 && BitConverter.ToUInt32(head) == Magic;
    }

    public static void Write(Stream stream, object root, GameTables tables)
    {
        var writer = new SaveWriter(stream, tables);
        writer.WriteHeader(Version);
        writer.Value(root);
        writer.Flush();
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
