using System.Text;

namespace OpenPax.Data.Installer;

/// <summary>
/// The parts of an Inno Setup 5.5.0 (Unicode) index the import needs
/// (docs/formats/inno-setup.md): the file list and the packed file table.
/// Everything before the file list is read past field by field, since
/// nothing in the index says how long each part is. Only this version is
/// read: it is the one GOG's offline installer for the game uses.
/// </summary>
internal sealed class SetupIndex
{
    public const string SupportedVersion = "Inno Setup Setup Data (5.5.0) (u)";

    private const int HeaderTexts = 31;
    private const int HeaderFlagCount = 46;
    // Which of those options says the files are encrypted.
    private const int EncryptionUsedFlag = 37;
    private const int FileFlagCount = 32;
    private const int DataFlagCount = 10;

    private SetupIndex(IReadOnlyList<FileEntry> files, IReadOnlyList<DataEntry> data, int compression)
    {
        Files = files;
        Data = data;
        Compression = compression;
    }

    public IReadOnlyList<FileEntry> Files { get; }

    public IReadOnlyList<DataEntry> Data { get; }

    /// <summary>How the files are packed: 0 stored, 1 zlib, 2 bzip2, 3 LZMA, 4 LZMA2.</summary>
    public int Compression { get; }

    /// <summary>Reads the index at <paramref name="offset"/> in the installer.</summary>
    public static SetupIndex Read(Stream file, long offset)
    {
        file.Position = offset;
        var id = new byte[64];
        file.ReadExactly(id);
        var version = Encoding.ASCII.GetString(id).TrimEnd('\0');
        if (version != SupportedVersion)
        {
            throw new InvalidDataException(string.Format(Strings.InnoVersion, version));
        }

        IndexCounts counts;
        int compression;
        var files = new List<FileEntry>();
        using (var main = IndexBlock.Open(file))
        {
            var r = new IndexReader(main);
            (counts, compression) = Header(r);
            PassOver(r, counts);
            for (var i = 0; i < counts.Files; i++)
            {
                files.Add(ReadFile(r));
            }

            // Icons, registry and the rest follow: not needed.
        }

        var data = new List<DataEntry>((int)counts.Data);
        using (var second = IndexBlock.Open(file))
        {
            var r = new IndexReader(second);
            for (var i = 0; i < counts.Data; i++)
            {
                data.Add(ReadData(r));
            }
        }

        return new SetupIndex(files, data, compression);
    }

    private static (IndexCounts Counts, int Compression) Header(IndexReader r)
    {
        // Names, URLs, folders, the license and the compiled script.
        r.SkipTexts(HeaderTexts);
        var c = new IndexCounts(
            Languages: r.U32(),
            Messages: r.U32(),
            Permissions: r.U32(),
            Types: r.U32(),
            Components: r.U32(),
            Tasks: r.U32(),
            Directories: r.U32(),
            Files: r.U32(),
            Data: r.U32());
        r.Skip(7 * 4); // icon, ini, registry, delete, uninstall delete, run and uninstall run counts
        if (c.Files > 100_000 || c.Data > 100_000 || c.Messages > 100_000)
        {
            throw new InvalidDataException(Strings.InnoDamaged);
        }

        r.SkipWindowsVersions();
        r.Skip(3 * 4); // colors
        r.Skip(20 + 8); // password hash and salt
        r.Skip(8 + 4); // extra disk space, slices per disk
        r.Skip(5); // uninstall log mode, folder exists warning, privileges, language dialog, language detection
        var compression = r.U8();
        r.Skip(2); // architectures allowed, and installed in 64-bit mode
        r.Skip(2); // whether the folder and group pages show
        r.Skip(8); // uninstall display size
        var options = r.Flags(HeaderFlagCount);
        if ((options & (1UL << EncryptionUsedFlag)) != 0)
        {
            throw new InvalidDataException(Strings.InnoEncrypted);
        }

        return (c, compression);
    }

    // The entries between the header and the file list, in the order the index keeps them.
    private static void PassOver(IndexReader r, IndexCounts c)
    {
        for (var i = 0; i < c.Languages; i++)
        {
            r.SkipTexts(10); // names, fonts, language data, license and info texts
            r.Skip(4 + (4 * 4) + 1); // language id, font sizes, right to left
        }

        for (var i = 0; i < c.Messages; i++)
        {
            r.SkipTexts(2);
            r.Skip(4);
        }

        for (var i = 0; i < c.Permissions; i++)
        {
            r.SkipTexts(1);
        }

        for (var i = 0; i < c.Types; i++)
        {
            r.SkipTexts(4);
            r.SkipWindowsVersions();
            r.Flags(1);
            r.Skip(1 + 8); // type, size
        }

        for (var i = 0; i < c.Components; i++)
        {
            r.SkipTexts(5);
            r.Skip(8 + 4 + 1); // extra disk space, level, used
            r.SkipWindowsVersions();
            r.Flags(5);
            r.Skip(8); // size
        }

        for (var i = 0; i < c.Tasks; i++)
        {
            r.SkipTexts(6);
            r.Skip(4 + 1); // level, used
            r.SkipWindowsVersions();
            r.Flags(5);
        }

        for (var i = 0; i < c.Directories; i++)
        {
            r.SkipTexts(1 + 6); // name, then the conditions
            r.Skip(4); // attributes
            r.SkipWindowsVersions();
            r.Skip(2); // permission
            r.Flags(5);
        }
    }

    private static FileEntry ReadFile(IndexReader r)
    {
        r.SkipTexts(1); // source, for files taken from beside the installer
        var destination = r.Text();
        r.SkipTexts(2); // font name, assembly name
        r.SkipTexts(3); // components, tasks, languages
        var check = r.Text();
        r.SkipTexts(2); // after and before install
        r.SkipWindowsVersions();
        var location = r.U32();
        r.Skip(4 + 8 + 2); // attributes, external size, permission
        r.Flags(FileFlagCount);
        var type = r.U8();
        return new FileEntry(destination, location, check, type == 0);
    }

    private static DataEntry ReadData(IndexReader r)
    {
        r.Skip(8); // first and last slice: one file, so both 0
        long chunkOffset = r.U32();
        var inChunk = (long)r.U64();
        var size = (long)r.U64();
        var chunkSize = (long)r.U64();
        var sha1 = r.Bytes(20);
        r.Skip(8 + 8); // time, file version
        var flags = r.Flags(DataFlagCount);
        if (size < 0 || inChunk < 0 || chunkSize < 0)
        {
            throw new InvalidDataException(Strings.InnoDamaged);
        }

        // The options: version info valid, not valid, time in UTC, uninstaller,
        // calls filtered, touch, chunk encrypted, chunk compressed, solid break.
        return new DataEntry(chunkOffset, inChunk, size, chunkSize, sha1,
            CallsFiltered: (flags & (1 << 4)) != 0,
            Compressed: (flags & (1 << 7)) != 0,
            Encrypted: (flags & (1 << 6)) != 0);
    }
}
