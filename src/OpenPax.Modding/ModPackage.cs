using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;

namespace OpenPax.Modding;

/// <summary>
/// A mod's folder as one file, to send to another player (docs/modding/multiplayer.md):
/// a zip of every file under it, in path order with a fixed time, so the
/// same folder always makes the same bytes. A mod's hash is over its files,
/// not the zip, so a folder and a package of it have the same one.
/// </summary>
public static class ModPackage
{
    /// <summary>The largest package taken, and the most a package may unpack to: a mod bigger than this is not sent.</summary>
    public const long MaxBytes = 64L * 1024 * 1024;

    // Every entry gets this time, so a package does not change with the files' dates.
    private static readonly DateTimeOffset _fixedTime = new(2000, 1, 1, 0, 0, 0, TimeSpan.Zero);

    /// <summary>SHA-256 over each file's path from the folder (with "/") and its bytes, in ordinal path order; 64 hex digits.</summary>
    public static string HashOf(string folder)
    {
        using var sha = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        foreach (var (relative, path) in Files(folder))
        {
            var name = Encoding.UTF8.GetBytes(relative);
            sha.AppendData(BitConverter.GetBytes(name.Length));
            sha.AppendData(name);
            var bytes = File.ReadAllBytes(path);
            sha.AppendData(BitConverter.GetBytes((long)bytes.Length));
            sha.AppendData(bytes);
        }

        return Convert.ToHexStringLower(sha.GetHashAndReset());
    }

    /// <summary>The folder as a package.</summary>
    public static byte[] Pack(string folder)
    {
        using var stream = new MemoryStream();
        using (var zip = new ZipArchive(stream, ZipArchiveMode.Create, leaveOpen: true))
        {
            foreach (var (relative, path) in Files(folder))
            {
                var entry = zip.CreateEntry(relative, CompressionLevel.Optimal);
                entry.LastWriteTime = _fixedTime;
                using var to = entry.Open();
                using var from = File.OpenRead(path);
                from.CopyTo(to);
            }
        }

        return stream.ToArray();
    }

    /// <summary>
    /// A package unpacked into a new folder, which must not exist yet. Only
    /// plain files under the folder are written (no paths out of it), no
    /// more than <see cref="MaxBytes"/> in all. The hash of what was written
    /// must begin with the one expected (a mod key's 12 digits, or all 64),
    /// or the folder is removed and false given.
    /// </summary>
    public static bool Unpack(byte[] package, string folder, string expectedHash)
    {
        if (package.Length > MaxBytes || Directory.Exists(folder))
        {
            return false;
        }

        var root = Path.GetFullPath(folder) + Path.DirectorySeparatorChar;
        try
        {
            Directory.CreateDirectory(folder);
            using var zip = new ZipArchive(new MemoryStream(package), ZipArchiveMode.Read);
            var written = 0L;
            foreach (var entry in zip.Entries)
            {
                if (entry.FullName.EndsWith('/'))
                {
                    continue;
                }

                var target = Path.GetFullPath(Path.Combine(folder, entry.FullName));
                written += entry.Length;
                if (!target.StartsWith(root, StringComparison.Ordinal) || written > MaxBytes)
                {
                    throw new InvalidDataException(entry.FullName);
                }

                Directory.CreateDirectory(Path.GetDirectoryName(target) ?? folder);
                entry.ExtractToFile(target);
            }

            if (HashOf(folder).StartsWith(expectedHash, StringComparison.Ordinal) && expectedHash.Length > 0)
            {
                return true;
            }
        }
        catch (Exception e) when (e is InvalidDataException or IOException or UnauthorizedAccessException)
        {
        }

        Directory.Delete(folder, true);
        return false;
    }

    private static IEnumerable<(string Relative, string Path)> Files(string folder) =>
        Directory.EnumerateFiles(folder, "*", SearchOption.AllDirectories)
            .Select(p => (Relative: Path.GetRelativePath(folder, p).Replace('\\', '/'), Path: p))
            .OrderBy(f => f.Relative, StringComparer.Ordinal);
}
