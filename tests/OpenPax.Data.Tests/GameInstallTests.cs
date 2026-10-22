using System.Security.Cryptography;
using OpenPax.Data.Install;

namespace OpenPax.Data.Tests;

public sealed class GameInstallTests : IDisposable
{
    private readonly string _dir = Directory.CreateTempSubdirectory("openpax-install-").FullName;

    public void Dispose()
    {
        Directory.Delete(_dir, recursive: true);
    }

    [Fact]
    public void ReportsMissingAndDifferentFiles()
    {
        byte[] good = [1, 2, 3];
        File.WriteAllBytes(Path.Combine(_dir, "a"), good);
        File.WriteAllBytes(Path.Combine(_dir, "b"), [9, 9, 9]);
        KnownFile[] known =
        [
            new("a", 3, Hex(good)),
            new("b", 3, Hex(good)),
            new("c", 3, Hex(good)),
        ];

        var result = GameInstall.Verify(_dir, known);

        Assert.False(result.IsGogBuild);
        Assert.Equal(["c"], result.Missing);
        Assert.Equal(["b"], result.Different);
        Assert.Contains("not the GOG build", result.Describe());
    }

    [Fact]
    public void ResolvesTheGameFolderFromItsParent()
    {
        var game = Directory.CreateDirectory(Path.Combine(_dir, GameInstall.FolderName)).FullName;
        File.WriteAllBytes(Path.Combine(game, GameInstall.Executable), []);

        Assert.Equal(game, GameInstall.Resolve(_dir));
        Assert.Equal(game, GameInstall.Resolve(game));
        Assert.Null(GameInstall.Resolve(Path.Combine(_dir, "elsewhere")));
    }

    [Fact]
    public void TheGogInstallPassesTheCheck()
    {
        var result = GameInstall.Verify(GameData.RequireFolder());
        Assert.True(result.IsGogBuild, result.Describe());
    }

    private static string Hex(byte[] data)
    {
        return Convert.ToHexStringLower(SHA256.HashData(data));
    }
}
