using System.Text;
using OpenPax.Data.Install;
using OpenPax.TestSupport;
using Xunit;

namespace OpenPax.Data.Tests;

public class InstallerCheckTests
{
    [Fact]
    public void BuiltOfflineInstallerIsOffline()
    {
        var mark = new byte[] { 0x72, 0x44, 0x6C, 0x50, 0x74, 0x53, 0xCD, 0xE6, 0xD7, 0x7B, 0x0B, 0x2A };
        var file = new byte[4096];
        file[0] = (byte)'M';
        file[1] = (byte)'Z';
        mark.CopyTo(file, 100);
        BitConverter.GetBytes(3000u).CopyTo(file, 100 + 12 + 20);
        Encoding.ASCII.GetBytes("Inno Setup Setup Data (5.5.0) (u)").CopyTo(file, 3000);
        Assert.Equal(InstallerKind.Offline, Check(file));
    }

    [Fact]
    public void TableWithoutSetupDataIsNotOffline()
    {
        var file = new byte[4096];
        file[0] = (byte)'M';
        file[1] = (byte)'Z';
        new byte[] { 0x72, 0x44, 0x6C, 0x50, 0x74, 0x53, 0xCD, 0xE6, 0xD7, 0x7B, 0x0B, 0x2A }.CopyTo(file, 100);
        BitConverter.GetBytes(3000u).CopyTo(file, 100 + 12 + 20);
        Assert.Equal(InstallerKind.Unknown, Check(file));
    }

    [Fact]
    public void BuiltStubIsStub()
    {
        var file = new byte[4096];
        file[0] = (byte)'M';
        file[1] = (byte)'Z';
        Encoding.Unicode.GetBytes("GOG Galaxy - Game Installer.exe").CopyTo(file, 500);
        Assert.Equal(InstallerKind.GalaxyStub, Check(file));
    }

    [Fact]
    public void NotAProgramIsUnknown()
    {
        Assert.Equal(InstallerKind.Unknown, Check(Encoding.ASCII.GetBytes("hello")));
        Assert.Equal(InstallerKind.Unknown, InstallerCheck.Identify(Path.Combine(Path.GetTempPath(), "openpax-no-such-file.exe")));
    }

    [Fact]
    public void GogFilesAreTold()
    {
        // GOG's installers where the game is set (OPENPAX_GAME), or beside the game folder.
        var offline = GameData.Installers("setup_pax_imperia_*.exe");
        var stub = GameData.Installers("GOG_Galaxy_*.exe");
        Assert.SkipWhen(offline.Length == 0 && stub.Length == 0, "No GOG installers beside your game folder.");

        foreach (var f in offline)
        {
            Assert.Equal(InstallerKind.Offline, InstallerCheck.Identify(f));
        }

        foreach (var f in stub)
        {
            Assert.Equal(InstallerKind.GalaxyStub, InstallerCheck.Identify(f));
        }
    }

    private static InstallerKind Check(byte[] bytes)
    {
        var path = Path.Combine(Path.GetTempPath(), $"openpax-installer-{Guid.NewGuid():N}.exe");
        File.WriteAllBytes(path, bytes);
        try
        {
            return InstallerCheck.Identify(path);
        }
        finally
        {
            File.Delete(path);
        }
    }
}
