using System.IO.Compression;

namespace OpenPax.Modding.Tests;

/// <summary>Mods as packages and keys (docs/modding/multiplayer.md): what a room's host sends and its members load.</summary>
public sealed class ModPackageTests
{
    private static TempFolder Mod(out string folder, string id = "pack", string version = "1")
    {
        var temp = new TempFolder();
        temp.Write("mods/" + id + "/mod.toml", $"id = \"{id}\"\nname = \"Pack\"\nversion = \"{version}\"\n");
        temp.Write("mods/" + id + "/data/art/main/1.png", "picture");
        temp.Write("mods/" + id + "/content/a.toml", "x = 1\n");
        folder = Path.Combine(temp.Path, "mods", id);
        return temp;
    }

    [Fact]
    public void APackageUnpacksToTheSameFilesAndHash()
    {
        using var temp = Mod(out var folder);
        var hash = ModPackage.HashOf(folder);
        var package = ModPackage.Pack(folder);
        Assert.Equal(package, ModPackage.Pack(folder)); // the same bytes every time

        var copy = Path.Combine(temp.Path, "synced", "pack-x");
        Assert.True(ModPackage.Unpack(package, copy, hash[..ModKey.HashDigits]));
        Assert.Equal(hash, ModPackage.HashOf(copy));
        Assert.Equal("picture", File.ReadAllText(Path.Combine(copy, "data", "art", "main", "1.png")));
    }

    [Fact]
    public void APackageThatIsNotWhatItSaysIsRefused()
    {
        using var temp = Mod(out var folder);
        var package = ModPackage.Pack(folder);
        var wrong = Path.Combine(temp.Path, "synced", "wrong");
        Assert.False(ModPackage.Unpack(package, wrong, "ffffffffffff"));
        Assert.False(Directory.Exists(wrong));

        // An entry that climbs out of the folder.
        using var stream = new MemoryStream();
        using (var zip = new ZipArchive(stream, ZipArchiveMode.Create, leaveOpen: true))
        {
            using var w = new StreamWriter(zip.CreateEntry("../escape.txt").Open());
            w.Write("no");
        }

        var target = Path.Combine(temp.Path, "synced", "evil");
        Assert.False(ModPackage.Unpack(stream.ToArray(), target, "0"));
        Assert.False(File.Exists(Path.Combine(temp.Path, "synced", "escape.txt")));
    }

    [Fact]
    public void AKeyNamesIdVersionAndTheStartOfTheHash()
    {
        using var temp = Mod(out var folder, "simple-combat", "1.2");
        var info = new ModInfo(ModManifest.Parse(File.ReadAllText(Path.Combine(folder, "mod.toml"))), folder);
        var key = info.Key;
        Assert.Equal(("simple-combat", "1.2", ModKey.HashDigits), (key.Id, key.Version, key.Hash.Length));
        Assert.Equal(key, ModKey.Parse(key.ToString()));
        Assert.True(key.Matches(info));
        Assert.Equal([key, key], ModKey.ParseSet(key + ", bad," + key));

        File.WriteAllText(Path.Combine(folder, "content", "a.toml"), "x = 2\n");
        Assert.False(key.Matches(new ModInfo(info.Manifest, folder))); // changed files, another mod
    }

    [Fact]
    public void ASetLoadsByKeyFromAnyFolderAndTellsWhatIsMissing()
    {
        using var temp = Mod(out var folder);
        var key = new ModInfo(ModManifest.Parse(File.ReadAllText(Path.Combine(folder, "mod.toml"))), folder).Key;
        var synced = Path.Combine(temp.Path, "synced");
        Assert.True(ModPackage.Unpack(ModPackage.Pack(folder), Path.Combine(synced, "pack-" + key.Hash), key.Hash));
        Directory.Delete(folder, true); // only the fetched copy is left

        var absent = new ModKey("gone", "1", "aaaaaaaaaaaa");
        var host = ModHost.Load([Path.Combine(temp.Path, "mods"), synced], [key, absent], out var missing);

        Assert.Equal([absent], missing);
        Assert.Equal(key.ToString(), host.Fingerprint);
        host.Unload();
    }
}
