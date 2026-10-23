using System.Formats.Tar;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using OpenPax.Host.Update;
using OpenPax.Ui;

namespace OpenPax.Host.Tests;

/// <summary>Updates from GitHub (docs/updates.md): which release, its download, and putting it in place.</summary>
public sealed class UpdateTests : IDisposable
{
    private const string Platform = "linux-x64";
    private readonly string _root = Path.Combine(Path.GetTempPath(), $"openpax-update-{Guid.NewGuid():N}");

    public UpdateTests() => Directory.CreateDirectory(_root);

    public void Dispose() => Directory.Delete(_root, recursive: true);

    private static ReleaseVersion V(string text) => ReleaseVersion.Parse(text) ?? throw new FormatException(text);

    [Fact]
    public void VersionsSortAsReleasesDo()
    {
        Assert.True(V("v0.1.10") > V("0.1.9"));
        Assert.True(V("0.2.0-beta.1") < V("0.2.0"));
        Assert.True(V("0.2.0-beta.2") > V("0.2.0-beta.1"));
        Assert.True(V("0.2.0-beta.10") > V("0.2.0-beta.9"));
        Assert.True(V("0.2.0-alpha") < V("0.2.0-beta"));
        Assert.True(V("0.1.2+abc123") == V("0.1.2"));
        Assert.True(V("0.0.0-dev").IsDevelopment);
        Assert.Equal("0.2.0-beta.1", V("v0.2.0-beta.1").ToString());
        Assert.Null(ReleaseVersion.Parse("0.1"));
        Assert.Null(ReleaseVersion.Parse("v1.x.0"));
        Assert.Null(ReleaseVersion.Parse("1.0.0-"));
    }

    private static string Asset(string version, string platform = Platform, string? digest = "sha256:ABCDEF", string? url = null) =>
        $$"""
        {"name":"OpenPax-{{version}}-{{platform}}.{{(platform.StartsWith("win", StringComparison.Ordinal) ? "zip" : "tar.gz")}}","size":10,
         "browser_download_url":"{{url ?? $"https://github.com/jlac1024/OpenPax/releases/download/v{version}/OpenPax-{version}-{platform}.tar.gz"}}"
         {{(digest is null ? "" : $",\"digest\":\"{digest}\"")}}}
        """;

    private static string Release(string version, bool pre = false, bool draft = false, params string[] assets) =>
        $$"""{"tag_name":"v{{version}}","draft":{{(draft ? "true" : "false")}},"prerelease":{{(pre ? "true" : "false")}},"html_url":"https://github.com/jlac1024/OpenPax/releases/tag/v{{version}}","assets":[{{string.Join(",", assets)}}]}""";

    [Fact]
    public void TheNewestAllowedReleaseIsPicked()
    {
        var json = "[" + string.Join(",",
            Release("0.3.0", draft: true, assets: Asset("0.3.0")),
            Release("0.2.1", pre: true, assets: Asset("0.2.1")),
            Release("0.2.0", assets: Asset("0.2.0")),
            Release("0.1.9", assets: Asset("0.1.9"))) + "]";

        var withPre = ReleaseFeed.Newest(json, V("0.1.2"), preReleases: true, Platform);
        Assert.Equal(V("0.2.1"), withPre?.Version);
        Assert.True(withPre?.IsPreRelease);
        Assert.Equal("abcdef", withPre?.Sha256);

        var stable = ReleaseFeed.Newest(json, V("0.1.2"), preReleases: false, Platform);
        Assert.Equal(V("0.2.0"), stable?.Version);
        Assert.False(stable?.IsPreRelease);

        Assert.Null(ReleaseFeed.Newest(json, V("0.2.1"), preReleases: true, Platform));
        Assert.Null(ReleaseFeed.Newest(json, V("0.2.0"), preReleases: false, Platform));
    }

    [Fact]
    public void AReleaseWithoutAFitBundleIsPassedOver()
    {
        var json = "[" + string.Join(",",
            Release("0.5.0", assets: Asset("0.5.0", platform: "osx-arm64")),
            Release("0.4.0", assets: Asset("0.4.0", digest: null)),
            Release("0.3.0", assets: Asset("0.3.0", url: "https://example.com/OpenPax-0.3.0-linux-x64.tar.gz")),
            Release("0.2.0", assets: Asset("0.2.0"))) + "]";

        // No bundle for this platform, no SHA-256, a download from elsewhere: only 0.2.0 qualifies.
        Assert.Equal(V("0.2.0"), ReleaseFeed.Newest(json, V("0.1.0"), preReleases: true, Platform)?.Version);
        Assert.Null(ReleaseFeed.Newest("{\"message\":\"rate limited\"}", V("0.1.0"), true, Platform));
    }

    [Fact]
    public void TheMacInstallIsTheFolderAroundTheApp()
    {
        var mac = Path.Combine(_root, "OpenPax-0.1.3-osx-arm64", "OpenPax.app", "Contents", "MacOS") + Path.DirectorySeparatorChar;
        Assert.Equal(Path.Combine(_root, "OpenPax-0.1.3-osx-arm64"), InstallFolder.Of(mac));
        Assert.Equal(_root, InstallFolder.Of(_root + Path.DirectorySeparatorChar));
        Assert.True(InstallFolder.Writable(_root));
    }

    [Fact]
    public void APackagedInstallNamesItsPackage()
    {
        Assert.Null(InstallFolder.Packaged(_root));
        File.WriteAllText(Path.Combine(_root, InstallFolder.PackagedMark), "Flatpak\n");
        Assert.Equal("Flatpak", InstallFolder.Packaged(_root));
    }

    // A bundle as tools/package.sh makes it: one folder, the program and its files.
    private static byte[] Bundle(string version, Dictionary<string, string> files, bool zip)
    {
        var top = $"OpenPax-{version}-{Platform}/";
        var output = new MemoryStream();
        if (zip)
        {
            using var archive = new ZipArchive(output, ZipArchiveMode.Create, leaveOpen: true);
            foreach (var (name, text) in files)
            {
                using var entry = archive.CreateEntry(top + name).Open();
                entry.Write(Encoding.UTF8.GetBytes(text));
            }
        }
        else
        {
            using var gzip = new GZipStream(output, CompressionLevel.Fastest, leaveOpen: true);
            using var tar = new TarWriter(gzip);
            foreach (var (name, text) in files)
            {
                tar.WriteEntry(new PaxTarEntry(TarEntryType.RegularFile, top + name)
                {
                    DataStream = new MemoryStream(Encoding.UTF8.GetBytes(text)),
                    Mode = UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute,
                });
            }
        }

        return output.ToArray();
    }

    private static ReleaseInfo Info(string version, byte[] bundle, string? sha = null) =>
        new(V(version), true, "https://github.com/jlac1024/OpenPax/releases/tag/v" + version, $"OpenPax-{version}-{Platform}.tar.gz",
            $"https://github.com/jlac1024/OpenPax/releases/download/v{version}/OpenPax-{version}-{Platform}.tar.gz",
            bundle.Length, sha ?? Convert.ToHexStringLower(SHA256.HashData(bundle)));

    private string Install(Dictionary<string, string> files)
    {
        var install = Directory.CreateDirectory(Path.Combine(_root, "install")).FullName;
        foreach (var (name, text) in files)
        {
            var path = Path.Combine(install, name);
            Directory.CreateDirectory(Path.GetDirectoryName(path)!); // a path in the folder always has one
            File.WriteAllText(path, text);
        }

        return install;
    }

    [Fact]
    public async Task ADownloadThatDoesNotMatchIsRefused()
    {
        var bundle = Bundle("0.2.0", new() { ["openpax"] = "new" }, zip: false);
        var info = Info("0.2.0", bundle, sha: new string('0', 64));
        using var github = new FakeGitHub();
        github.Files[info.AssetUrl] = bundle;
        using var http = new HttpClient(github);
        var install = Install([]);

        await Assert.ThrowsAsync<InvalidDataException>(() => UpdateInstaller.DownloadAsync(http, info, install, null, TestContext.Current.CancellationToken));
        Assert.Empty(Directory.GetFiles(Path.Combine(install, UpdateInstaller.WorkFolder)));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task AnUpdateGoesInAndKeepsThePlayersFiles(bool zip)
    {
        var install = Install(new()
        {
            ["openpax"] = "old program",
            ["obsolete.dll"] = "old",
            ["config.json"] = "edited by hand",
            ["mods/simple-combat/mod.toml"] = "old sample",
            ["mods/mine/mod.toml"] = "the player's own",
            ["data/import.toml"] = "the player's import",
        });
        var bundle = Bundle("0.2.0", new()
        {
            ["openpax"] = "new program",
            ["config.json"] = "shipped",
            ["mods/simple-combat/mod.toml"] = "new sample",
            ["lobby-server/openpax-lobby"] = "new server",
        }, zip);
        var info = Info("0.2.0", bundle);
        using var github = new FakeGitHub();
        github.Files[info.AssetUrl] = bundle;
        using var http = new HttpClient(github);

        var progress = new List<int>();
        var archive = await UpdateInstaller.DownloadAsync(http, info, install, new DownloadProgress(progress.Add), TestContext.Current.CancellationToken);
        UpdateInstaller.Swap(install, UpdateInstaller.Unpack(zip ? Rename(archive, ".zip") : archive));

        string Read(string name) => File.ReadAllText(Path.Combine(install, name));
        Assert.Equal("new program", Read("openpax"));
        Assert.Equal("new sample", Read("mods/simple-combat/mod.toml"));
        Assert.Equal("new server", Read("lobby-server/openpax-lobby"));
        Assert.Equal("edited by hand", Read("config.json"));
        Assert.Equal("the player's own", Read("mods/mine/mod.toml"));
        Assert.Equal("the player's import", Read("data/import.toml"));
        Assert.Equal("old program", File.ReadAllText(Path.Combine(install, UpdateInstaller.OldFolder, "openpax")));
        Assert.Equal(100, progress[^1]);
        if (!OperatingSystem.IsWindows() && !zip)
        {
            Assert.True(File.GetUnixFileMode(Path.Combine(install, "openpax")).HasFlag(UnixFileMode.UserExecute));
        }

        UpdateInstaller.CleanUp(install);
        Assert.False(Directory.Exists(Path.Combine(install, UpdateInstaller.OldFolder)));
        Assert.False(Directory.Exists(Path.Combine(install, UpdateInstaller.WorkFolder)));
    }

    private static string Rename(string path, string extension)
    {
        var renamed = Path.ChangeExtension(Path.ChangeExtension(path, null), null) + extension;
        File.Move(path, renamed);
        return renamed;
    }

    [Fact]
    public async Task TheUpdaterChecksDownloadsAndSaysWhenToRestart()
    {
        var install = Install(new() { ["openpax"] = "old program" });
        var bundle = Bundle("0.2.0", new() { ["openpax"] = "new program" }, zip: false);
        var info = Info("0.2.0", bundle);
        using var github = new FakeGitHub();
        github.Files[ReleaseFeed.ListUrl] = Encoding.UTF8.GetBytes("[" + Release("0.2.0", pre: true, assets:
            $$"""{"name":"{{info.AssetName}}","size":{{bundle.Length}},"browser_download_url":"{{info.AssetUrl}}","digest":"sha256:{{info.Sha256}}"}""") + "]");
        github.Files[info.AssetUrl] = bundle;
        using var updater = new Updater(install, github) { Running = V("0.1.2") };
        var state = new UpdateState();

        updater.Check(preReleases: false, say: true);
        await Settle(updater, state);
        Assert.Null(state.Available);
        Assert.Contains("newest", state.Status, StringComparison.Ordinal);

        updater.Check(preReleases: true, say: true);
        await Settle(updater, state);
        Assert.Equal(Platform, ReleaseFeed.Platform);
        Assert.Equal("0.2.0", state.Available);
        Assert.Equal("0.1.2", state.Running);

        updater.Install();
        var restart = await Settle(updater, state);
        Assert.True(restart);
        Assert.Equal("new program", File.ReadAllText(Path.Combine(install, "openpax")));
    }

    private static async Task<bool> Settle(Updater updater, UpdateState state)
    {
        for (var i = 0; i < 500; i++)
        {
            var restart = updater.Follow(state);
            if (!state.Busy)
            {
                return restart;
            }

            await Task.Delay(10, TestContext.Current.CancellationToken);
        }

        throw new TimeoutException();
    }
}
