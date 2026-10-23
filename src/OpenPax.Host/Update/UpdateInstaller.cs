using System.Formats.Tar;
using System.IO.Compression;
using System.Security.Cryptography;

namespace OpenPax.Host.Update;

/// <summary>
/// Puts a release bundle in place of the running game (docs/updates.md).
/// The download is checked against the SHA-256 GitHub gives it, unpacked
/// into a folder beside the install, then swapped in entry by entry: what
/// was there moves into a folder of old files first. A running program may
/// not be overwritten on Windows but may be renamed, so this works there
/// while the game runs; the old files are deleted on the next start. Any
/// step that fails puts back what was moved.
/// </summary>
internal static class UpdateInstaller
{
    public const string WorkFolder = ".openpax-update";
    public const string OldFolder = ".openpax-old";

    // Kept from the install as it was: config.json is edited by hand (the
    // lobby's address). Mods the player added beside the shipped ones are
    // carried into the new mods folder.
    private const string HandEdited = "config.json";
    private const string ModsFolder = "mods";

    /// <summary>Downloads the bundle into the install's work folder, checking its size and SHA-256 as it comes.</summary>
    public static async Task<string> DownloadAsync(HttpClient http, ReleaseInfo release, string install, IProgress<double>? progress, CancellationToken cancel)
    {
        var work = Directory.CreateDirectory(Path.Combine(install, WorkFolder)).FullName;
        var target = Path.Combine(work, release.AssetName);
        using var response = await http.GetAsync(release.AssetUrl, HttpCompletionOption.ResponseHeadersRead, cancel).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();
        using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        var buffer = new byte[1 << 16];
        long received = 0;
        await using (var input = await response.Content.ReadAsStreamAsync(cancel).ConfigureAwait(false))
        await using (var output = File.Create(target))
        {
            int n;
            while ((n = await input.ReadAsync(buffer, cancel).ConfigureAwait(false)) > 0)
            {
                received += n;
                if (received > release.Size)
                {
                    throw new InvalidDataException(Strings.UpdateBadDownload);
                }

                hash.AppendData(buffer, 0, n);
                await output.WriteAsync(buffer.AsMemory(0, n), cancel).ConfigureAwait(false);
                progress?.Report((double)received / Math.Max(1, release.Size));
            }
        }

        if (received != release.Size || Convert.ToHexStringLower(hash.GetHashAndReset()) != release.Sha256)
        {
            File.Delete(target);
            throw new InvalidDataException(Strings.UpdateBadDownload);
        }

        return target;
    }

    /// <summary>Unpacks the bundle beside it and gives the bundle's own folder (OpenPax-VERSION-PLATFORM) inside.</summary>
    public static string Unpack(string archive)
    {
        var into = Path.Combine(Path.GetDirectoryName(archive) ?? ".", "unpacked");
        if (Directory.Exists(into))
        {
            Directory.Delete(into, recursive: true);
        }

        Directory.CreateDirectory(into);
        if (archive.EndsWith(".zip", StringComparison.OrdinalIgnoreCase))
        {
            // Both readers refuse entries that would land outside the folder.
            ZipFile.ExtractToDirectory(archive, into);
        }
        else
        {
            using var file = File.OpenRead(archive);
            using var gzip = new GZipStream(file, CompressionMode.Decompress);
            TarFile.ExtractToDirectory(gzip, into, overwriteFiles: false);
        }

        var top = Directory.GetDirectories(into);
        if (top.Length != 1 || Directory.GetFiles(into).Length != 0)
        {
            throw new InvalidDataException(Strings.UpdateBadBundle);
        }

        return top[0];
    }

    /// <summary>
    /// Swaps the unpacked bundle in. Entries of the install the bundle does
    /// not have stay as they are (a data folder beside the program, the
    /// player's own files). On failure everything moved is put back.
    /// </summary>
    public static void Swap(string install, string bundle)
    {
        var old = Path.Combine(install, OldFolder);
        if (Directory.Exists(old))
        {
            Directory.Delete(old, recursive: true);
        }

        Directory.CreateDirectory(old);
        KeepPlayersFiles(install, bundle);

        var moved = new List<(string From, string To)>();
        try
        {
            foreach (var entry in Directory.EnumerateFileSystemEntries(bundle).Order(StringComparer.Ordinal))
            {
                var name = Path.GetFileName(entry);
                var current = Path.Combine(install, name);
                if (Exists(current))
                {
                    Move(current, Path.Combine(old, name));
                    moved.Add((Path.Combine(old, name), current));
                }

                Move(entry, current);
                moved.Add((current, entry));
            }
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            // Back as it was: the new entries out, the old ones home, last move first.
            for (var i = moved.Count - 1; i >= 0; i--)
            {
                var (from, to) = moved[i];
                try
                {
                    Move(from, to);
                }
                catch (Exception again) when (again is IOException or UnauthorizedAccessException)
                {
                    // One that will not go back is left where it is; the rest still do.
                }
            }

            throw;
        }
    }

    /// <summary>Deletes what an earlier update left: the old files and the download. Each is tried; none has to go.</summary>
    public static void CleanUp(string install)
    {
        foreach (var name in new[] { OldFolder, WorkFolder })
        {
            try
            {
                var path = Path.Combine(install, name);
                if (Directory.Exists(path))
                {
                    Directory.Delete(path, recursive: true);
                }
            }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException)
            {
                // Still in use (the old game not quite gone): the next start tries again.
            }
        }
    }

    // The hand-edited config and the player's own mods go into the bundle
    // before the swap, so they come through it.
    private static void KeepPlayersFiles(string install, string bundle)
    {
        var config = Path.Combine(install, HandEdited);
        if (File.Exists(config))
        {
            File.Copy(config, Path.Combine(bundle, HandEdited), overwrite: true);
        }

        var mods = Path.Combine(install, ModsFolder);
        var newMods = Path.Combine(bundle, ModsFolder);
        if (Directory.Exists(mods) && Directory.Exists(newMods))
        {
            foreach (var mod in Directory.GetDirectories(mods))
            {
                var name = Path.GetFileName(mod);
                if (!Directory.Exists(Path.Combine(newMods, name)))
                {
                    CopyFolder(mod, Path.Combine(newMods, name));
                }
            }
        }
    }

    private static void CopyFolder(string from, string to)
    {
        Directory.CreateDirectory(to);
        foreach (var file in Directory.GetFiles(from))
        {
            File.Copy(file, Path.Combine(to, Path.GetFileName(file)));
        }

        foreach (var folder in Directory.GetDirectories(from))
        {
            CopyFolder(folder, Path.Combine(to, Path.GetFileName(folder)));
        }
    }

    private static bool Exists(string path) => File.Exists(path) || Directory.Exists(path);

    private static void Move(string from, string to)
    {
        if (Directory.Exists(from))
        {
            Directory.Move(from, to);
        }
        else
        {
            File.Move(from, to);
        }
    }
}
