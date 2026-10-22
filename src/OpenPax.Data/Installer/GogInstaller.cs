using System.Security.Cryptography;
using OpenPax.Data.Compression;
using OpenPax.Data.Install;

namespace OpenPax.Data.Installer;

/// <summary>
/// GOG's offline installer for the game, read without running it
/// (docs/first-run.md, docs/formats/inno-setup.md): the files it would put
/// in the game's folder are unpacked into a folder of ours, which the
/// import then reads like an installed copy.
/// </summary>
public sealed class GogInstaller
{
    private const string AppFolder = "{app}\\";
    // GOG's installer keeps spare copies here for its own repair step.
    private const string SupportFolder = "{app}\\__support\\";
    private static readonly byte[] _chunkMark = "zlb\u001a"u8.ToArray();

    private readonly string _path;
    private readonly long _fileData;
    private readonly int _compression;
    private readonly IReadOnlyList<(string Path, DataEntry Data)> _files;

    private GogInstaller(string path, long fileData, int compression, IReadOnlyList<(string, DataEntry)> files)
    {
        _path = path;
        _fileData = fileData;
        _compression = compression;
        _files = files;
    }

    /// <summary>The game folder's files the installer holds, relative, with / between folders.</summary>
    public IEnumerable<string> Files => _files.Select(f => f.Path);

    /// <summary>Their size in all, unpacked.</summary>
    public long Size => _files.Sum(f => f.Data.Size);

    /// <summary>Reads the installer's index. Throws <see cref="InvalidDataException"/> with a plain message when it is not one this can read.</summary>
    public static GogInstaller Open(string path)
    {
        switch (InstallerCheck.Identify(path))
        {
            case InstallerKind.GalaxyStub:
                throw new InvalidDataException(Strings.InnoGalaxyStub);
            case InstallerKind.Unknown:
                throw new InvalidDataException(Strings.InnoNotInstaller);
        }

        using var file = File.OpenRead(path);
        var table = InstallerCheck.ReadTable(file) ?? throw new InvalidDataException(Strings.InnoNotInstaller);
        var index = SetupIndex.Read(file, table.SetupData);
        if (index.Compression is not (0 or 3 or 4))
        {
            throw new InvalidDataException(string.Format(Strings.InnoCompression, index.Compression));
        }

        return new GogInstaller(path, table.FileData, index.Compression, Pick(index));
    }

    /// <summary>
    /// Where several entries put a file in the same place, the installer's
    /// script chooses between them (checkIfOverwrite, IsWindows10 and the
    /// like). The last one with no check is the one always installed, and
    /// matches GOG's installed copy; failing that, the last one, since later
    /// entries overwrite earlier ones.
    /// </summary>
    private static List<(string, DataEntry)> Pick(SetupIndex index)
    {
        var chosen = new Dictionary<string, FileEntry>(StringComparer.OrdinalIgnoreCase);
        var order = new List<string>();
        foreach (var entry in index.Files)
        {
            if (!entry.IsUserFile || entry.Location >= index.Data.Count
                || !entry.Destination.StartsWith(AppFolder, StringComparison.OrdinalIgnoreCase)
                || entry.Destination.StartsWith(SupportFolder, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var relative = entry.Destination[AppFolder.Length..].Replace('\\', '/');
            if (!Safe(relative))
            {
                throw new InvalidDataException(Strings.InnoDamaged);
            }

            if (!chosen.TryGetValue(relative, out var before))
            {
                order.Add(relative);
                chosen[relative] = entry;
            }
            else if (entry.Check.Length == 0 || before.Check.Length != 0)
            {
                chosen[relative] = entry;
            }
        }

        return [.. order.Select(p => (p, index.Data[(int)chosen[p].Location]))];
    }

    // No climbing out of the folder the files go to.
    private static bool Safe(string relative) =>
        relative.Length > 0 && !relative.StartsWith('/') && !relative.Contains(':')
        && relative.Split('/').All(part => part.Length > 0 && part != "." && part != "..");

    /// <summary>
    /// Unpacks the game's files into <paramref name="folder"/>, checking
    /// each against the SHA-1 the installer gives it. Progress goes 0 to 1
    /// by bytes unpacked.
    /// </summary>
    public void Extract(string folder, IProgress<double>? progress = null, CancellationToken cancel = default)
    {
        var total = Math.Max(Size, 1);
        long done = 0;
        using var file = new FileStream(_path, FileMode.Open, FileAccess.Read, FileShare.Read, 1 << 16);
        var buffer = new byte[1 << 16];
        foreach (var (relative, data) in _files)
        {
            cancel.ThrowIfCancellationRequested();
            if (data.Encrypted)
            {
                throw new InvalidDataException(Strings.InnoEncrypted);
            }

            var target = Path.Combine(folder, relative);
            Directory.CreateDirectory(Path.GetDirectoryName(target) ?? folder);
            // The chunk readers own nothing: the installer file is closed once, below.
            var chunk = OpenChunk(file, data);
            Skip(chunk, data.OffsetInChunk, buffer);
            using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA1);
            using (var output = File.Create(target))
            {
                if (data.CallsFiltered)
                {
                    // Programs and libraries: small, and the filter looks ahead, so whole.
                    var whole = new byte[data.Size];
                    chunk.ReadExactly(whole);
                    CallFilter.Undo(whole);
                    hash.AppendData(whole);
                    output.Write(whole);
                    done += whole.Length;
                }
                else
                {
                    for (var left = data.Size; left > 0;)
                    {
                        var n = (int)Math.Min(left, buffer.Length);
                        chunk.ReadExactly(buffer, 0, n);
                        hash.AppendData(buffer, 0, n);
                        output.Write(buffer, 0, n);
                        left -= n;
                        done += n;
                        progress?.Report((double)done / total);
                    }
                }
            }

            if (!hash.GetHashAndReset().AsSpan().SequenceEqual(data.Sha1))
            {
                throw new InvalidDataException(string.Format(Strings.InnoBadFile, relative));
            }

            progress?.Report((double)done / total);
        }
    }

    private Stream OpenChunk(FileStream file, DataEntry data)
    {
        file.Position = _fileData + data.ChunkOffset;
        Span<byte> mark = stackalloc byte[4];
        file.ReadExactly(mark);
        if (!mark.SequenceEqual(_chunkMark))
        {
            throw new InvalidDataException(Strings.InnoDamaged);
        }

        if (!data.Compressed || _compression == 0)
        {
            return file;
        }

        var wanted = data.OffsetInChunk + data.Size;
        return _compression == 4 ? new Lzma2Stream(file, wanted) : new LzmaStream(file);
    }

    private static void Skip(Stream chunk, long count, byte[] buffer)
    {
        while (count > 0)
        {
            var n = (int)Math.Min(count, buffer.Length);
            chunk.ReadExactly(buffer, 0, n);
            count -= n;
        }
    }
}
