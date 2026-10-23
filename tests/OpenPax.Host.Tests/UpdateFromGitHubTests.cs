using OpenPax.Host.Update;
using OpenPax.Ui;

namespace OpenPax.Host.Tests;

/// <summary>
/// The real thing, against GitHub (docs/updates.md): an old version finds a
/// newer release, downloads its bundle and puts it in place. Opt-in, as it
/// downloads about 90 MB: set OPENPAX_NETWORK_TESTS=1.
/// </summary>
public sealed class UpdateFromGitHubTests
{
    [Fact]
    public async Task AnOldVersionUpdatesFromGitHub()
    {
        Assert.SkipUnless(Environment.GetEnvironmentVariable("OPENPAX_NETWORK_TESTS") == "1", "Network tests are off: set OPENPAX_NETWORK_TESTS=1");
        var install = Path.Combine(Path.GetTempPath(), $"openpax-github-{Guid.NewGuid():N}");
        Directory.CreateDirectory(install);
        try
        {
            File.WriteAllText(Path.Combine(install, "config.json"), "kept");
            using var updater = new Updater(install) { Running = ReleaseVersion.Parse("0.0.1") ?? default };
            var state = new UpdateState();
            updater.Check(preReleases: true, say: true);
            await Settle(updater, state);
            Assert.NotNull(state.Available);

            updater.Install();
            Assert.True(await Settle(updater, state), state.Status);
            Assert.True(File.Exists(InstallFolder.Program(install)));
            Assert.Equal("kept", File.ReadAllText(Path.Combine(install, "config.json")));
            Assert.True(Directory.Exists(Path.Combine(install, "lobby-server")));
        }
        finally
        {
            Directory.Delete(install, recursive: true);
        }
    }

    private static async Task<bool> Settle(Updater updater, UpdateState state)
    {
        for (var i = 0; i < 6000; i++)
        {
            var restart = updater.Follow(state);
            if (!state.Busy)
            {
                return restart;
            }

            await Task.Delay(50, TestContext.Current.CancellationToken);
        }

        throw new TimeoutException();
    }
}
