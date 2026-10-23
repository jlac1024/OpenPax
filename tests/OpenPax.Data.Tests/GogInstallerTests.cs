using System.Security.Cryptography;
using OpenPax.Data.Install;
using OpenPax.Data.Installer;
using OpenPax.TestSupport;

namespace OpenPax.Data.Tests;

/// <summary>Reading GOG's offline installer: what it unpacks must be the installed game, byte for byte.</summary>
public class GogInstallerTests
{
    private static string Installer()
    {
        var found = GameData.Installers("setup_pax_imperia_*.exe");
        Assert.SkipWhen(found.Length == 0, "GOG's offline installer not found: put setup_pax_imperia_2.0.0.12.exe beside your game folder, or point OPENPAX_GAME at it");
        return found[0];
    }

    [Fact]
    public void TheIndexListsTheGameFolder()
    {
        var installer = GogInstaller.Open(Installer());
        var files = installer.Files.ToList();
        Assert.Contains("Pax Imperia.exe", files);
        Assert.Contains("Data/PrefData", files);
        Assert.Contains("_Movie/PAX2.SMK", files);
        Assert.DoesNotContain(files, f => f.StartsWith("__support", StringComparison.Ordinal) || f.Contains('{'));
        Assert.Equal(files.Count, files.Distinct(StringComparer.OrdinalIgnoreCase).Count());
    }

    [Fact]
    public void TheStubIsRefusedWithAPlainMessage()
    {
        var stub = GameData.Installers("GOG_Galaxy_*.exe");
        Assert.SkipWhen(stub.Length == 0, "GOG's Galaxy stub not found beside your game folder");
        var ex = Assert.Throws<InvalidDataException>(() => GogInstaller.Open(stub[0]));
        Assert.Contains("Galaxy installer", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void ImportingTheStubSaysWhyAndLeavesNothing()
    {
        var stub = GameData.Installers("GOG_Galaxy_*.exe");
        Assert.SkipWhen(stub.Length == 0, "GOG's Galaxy stub not found beside your game folder");
        var root = GameData.RepositoryRoot();
        var data = Path.Combine(root, "build", "test-stub-import", "data");
        var log = new StringWriter();
        Assert.False(OpenPax.Data.Import.InstallerImport.Run(stub[0], data, null, log));
        Assert.Contains("Galaxy installer", log.ToString(), StringComparison.Ordinal);
        Assert.False(Directory.Exists(data));
        Assert.False(Directory.Exists(data + ".unpacking"));
    }

    [Fact]
    public void UnpackedFilesMatchTheInstalledGame()
    {
        var game = GameData.RequireFolder();
        var installer = GogInstaller.Open(Installer());
        // In build/, not the system temp folder, which may be held in memory.
        var root = GameData.RepositoryRoot();
        var output = Path.Combine(root, "build", "test-unpack");
        if (Directory.Exists(output))
        {
            Directory.Delete(output, true);
        }

        try
        {
            var reports = new List<double>();
            installer.Extract(output, new Inline(reports.Add), TestContext.Current.CancellationToken);
            Assert.Equal(1.0, reports[^1], 6);
            var differ = installer.Files
                .Where(f => !File.Exists(Path.Combine(game, f)) || Hash(Path.Combine(game, f)) != Hash(Path.Combine(output, f)))
                .ToList();
            // GOG's Galaxy client ships its own builds of its files, so a copy
            // installed through Galaxy differs there; the game's files never do.
            Assert.All(differ, f => Assert.True(f.StartsWith("goggame-", StringComparison.Ordinal) || f == "webcache.zip", f));
            Assert.True(GameInstall.Verify(output).IsGogBuild);
        }
        finally
        {
            Directory.Delete(output, true);
        }
    }

    private static string Hash(string path)
    {
        using var file = File.OpenRead(path);
        return Convert.ToHexStringLower(SHA256.HashData(file));
    }

    // Reports at once, on the thread that makes them (Progress<T> would post them later).
    private sealed class Inline(Action<double> report) : IProgress<double>
    {
        public void Report(double value) => report(value);
    }
}
