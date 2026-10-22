using System.Security.Cryptography;

namespace OpenPax.Data.Install;

/// <summary>Finds and checks the GOG install of Pax Imperia: Eminent Domain.</summary>
public static class GameInstall
{
    public const string FolderName = "Pax Imperia - Eminent Domain";
    public const string Executable = "Pax Imperia.exe";

    /// <summary>
    /// Accepts either the game folder itself or a folder that contains it
    /// (such as references/gog/). Returns null when neither holds the game.
    /// </summary>
    public static string? Resolve(string path)
    {
        if (File.Exists(Path.Combine(path, Executable)))
        {
            return Path.GetFullPath(path);
        }

        var inner = Path.Combine(path, FolderName);
        return File.Exists(Path.Combine(inner, Executable)) ? Path.GetFullPath(inner) : null;
    }

    /// <summary>Checks every file the port needs against the GOG build's sizes and hashes.</summary>
    public static VerifyResult Verify(string gameFolder)
    {
        return Verify(gameFolder, KnownFiles.All);
    }

    internal static VerifyResult Verify(string gameFolder, IReadOnlyList<KnownFile> known)
    {
        var missing = new List<string>();
        var different = new List<string>();
        foreach (var file in known)
        {
            var path = Path.Combine(gameFolder, file.Path);
            var info = new FileInfo(path);
            if (!info.Exists)
            {
                missing.Add(file.Path);
            }
            else if (info.Length != file.Size || Sha256Hex(path) != file.Sha256)
            {
                different.Add(file.Path);
            }
        }

        return new VerifyResult(gameFolder, missing, different);
    }

    private static string Sha256Hex(string path)
    {
        using var stream = File.OpenRead(path);
        return Convert.ToHexStringLower(SHA256.HashData(stream));
    }
}
