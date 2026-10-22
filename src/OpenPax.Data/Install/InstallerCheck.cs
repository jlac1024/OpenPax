using System.Buffers.Binary;
using System.Text;
using OpenPax.Data.Installer;

namespace OpenPax.Data.Install;

/// <summary>
/// Tells GOG's offline installer from its Galaxy stub without reading the
/// whole file (docs/first-run.md). Both are Windows programs named after the
/// game, so the name alone does not say which one the player picked.
/// </summary>
public static class InstallerCheck
{
    // Inno Setup's loader keeps a table of where its parts are, marked by
    // these 12 bytes, inside the setup program near the start of the file.
    private static readonly byte[] _tableMark = [0x72, 0x44, 0x6C, 0x50, 0x74, 0x53, 0xCD, 0xE6, 0xD7, 0x7B, 0x0B, 0x2A];
    private static readonly byte[] _setupDataMark = Encoding.ASCII.GetBytes("Inno Setup Setup Data (");

    // The stub's version info calls it this, in UTF-16 as Windows stores it.
    private static readonly byte[] _stubName = Encoding.Unicode.GetBytes("GOG Galaxy - Game Installer");

    // The loader table sits in the setup program's resources, well inside
    // this; the stub is under 1 MB in all.
    private const int SearchLength = 8 << 20;

    public static InstallerKind Identify(string path)
    {
        try
        {
            using var file = File.OpenRead(path);
            var head = new byte[(int)Math.Min(file.Length, SearchLength)];
            file.ReadExactly(head);
            if (head.Length < 2 || head[0] != 'M' || head[1] != 'Z')
            {
                return InstallerKind.Unknown;
            }

            if (FindTable(head) is { } table && HasSetupData(file, table.SetupData))
            {
                return InstallerKind.Offline;
            }

            return head.AsSpan().IndexOf(_stubName) >= 0 ? InstallerKind.GalaxyStub : InstallerKind.Unknown;
        }
        catch (IOException)
        {
            return InstallerKind.Unknown;
        }
        catch (UnauthorizedAccessException)
        {
            return InstallerKind.Unknown;
        }
    }

    /// <summary>The installer's table of parts, read from the start of the file; null when there is none.</summary>
    internal static LoaderTable? ReadTable(Stream file)
    {
        file.Position = 0;
        var head = new byte[(int)Math.Min(file.Length, SearchLength)];
        file.ReadExactly(head);
        return FindTable(head);
    }

    // The table: mark, then version, total size, setup program offset and
    // size and checksum, then the offsets of the setup data and of the
    // packed files (all 32-bit).
    private static LoaderTable? FindTable(byte[] head)
    {
        var at = head.AsSpan().IndexOf(_tableMark);
        if (at < 0 || at + _tableMark.Length + 28 > head.Length)
        {
            return null;
        }

        var table = head.AsSpan(at + _tableMark.Length);
        return new LoaderTable(BinaryPrimitives.ReadUInt32LittleEndian(table[20..]), BinaryPrimitives.ReadUInt32LittleEndian(table[24..]));
    }

    private static bool HasSetupData(FileStream file, long offset)
    {
        if (offset + _setupDataMark.Length > file.Length)
        {
            return false;
        }

        var found = new byte[_setupDataMark.Length];
        file.Position = offset;
        file.ReadExactly(found);
        return found.AsSpan().SequenceEqual(_setupDataMark);
    }
}
