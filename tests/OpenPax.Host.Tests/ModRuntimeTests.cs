using System.Runtime.CompilerServices;
using OpenPax.Modding;
using OpenPax.TestSupport;

namespace OpenPax.Host.Tests;

/// <summary>A mod set loaded whole, and another loaded in its place (docs/modding/multiplayer.md), with the real imported data.</summary>
public sealed class ModRuntimeTests : IDisposable
{
    private readonly string _mods = Path.Combine(Path.GetTempPath(), "openpax-runtime-" + Guid.NewGuid().ToString("N"));

    public ModRuntimeTests()
    {
        // Arsenal as it is in the repository, and Bounty from its build.
        var repo = new DirectoryInfo(AppContext.BaseDirectory);
        while (!File.Exists(Path.Combine(repo.FullName, "OpenPax.slnx")))
        {
            repo = repo.Parent!; // the tests run inside the repository
        }

        Copy(Path.Combine(repo.FullName, "samples", "Arsenal"), Path.Combine(_mods, "arsenal"));
        var bounty = Path.Combine(_mods, "bounty");
        Directory.CreateDirectory(bounty);
        var built = AppContext.BaseDirectory.Replace("OpenPax.Host.Tests", "Bounty", StringComparison.Ordinal);
        foreach (var name in new[] { "Bounty.dll", "mod.toml", "settings.toml" })
        {
            if (File.Exists(Path.Combine(built, name)))
            {
                File.Copy(Path.Combine(built, name), Path.Combine(bounty, name));
            }
        }
    }

    public void Dispose() => Directory.Delete(_mods, true);

    private static void Copy(string from, string to)
    {
        foreach (var file in Directory.GetFiles(from, "*", SearchOption.AllDirectories))
        {
            var target = Path.Combine(to, Path.GetRelativePath(from, file));
            Directory.CreateDirectory(Path.GetDirectoryName(target)!); // a file's folder
            File.Copy(file, target);
        }
    }

    [Fact]
    public void ARoomsSetLoadsInPlaceOfTheOwnAndBack()
    {
        var data = ImportedData.RequireFolder();
        var own = ModRuntime.Own(data, [_mods], []);
        var vanillaTechs = own.Launcher.Tables.Techs.Count;
        Assert.Empty(own.Mods.Mods);

        // The room's set by keys: Arsenal's content reaches the tables, the screens' tech names and records.
        var arsenal = ModCatalog.Find(_mods, []).Single(m => m.Manifest.Id == "arsenal");
        var room = ModRuntime.ForKeys(data, [_mods], [arsenal.Key], out var missing);
        Assert.Empty(missing);
        Assert.Empty(room.Problems);
        Assert.Equal(arsenal.Key.ToString(), room.Mods.Fingerprint);
        Assert.Equal(vanillaTechs + 3, room.Launcher.Tables.Techs.Count);
        var lance = room.Launcher.Tables.Techs[vanillaTechs];
        Assert.Equal("Storm Lance", room.Assets.Strings.Get(new OpenPax.Data.Views.TextRef(100000 + lance.Id, 0)));
        Assert.NotNull(room.Assets.Tech(lance.Id));

        // A key this machine does not have is told back.
        ModRuntime.ForKeys(data, [_mods], [new ModKey("arsenal", "1.0", "000000000000")], out var lacking);
        Assert.Single(lacking);
    }

    [Fact]
    public void ACodeModIsLetGoWhenAnotherSetTakesItsPlace()
    {
        var data = ImportedData.RequireFolder();
        Assert.SkipUnless(File.Exists(Path.Combine(_mods, "bounty", "Bounty.dll")), "Bounty is not built here");
        var gone = LoadAndUnload(data);
        for (var i = 0; i < 20 && gone.IsAlive; i++)
        {
            GC.Collect();
            GC.WaitForPendingFinalizers();
        }

        Assert.False(gone.IsAlive, "the mod's assemblies were let go");
    }

    // Kept out of line, so nothing of the mod stays reachable from the test's own frame.
    [MethodImpl(MethodImplOptions.NoInlining)]
    private WeakReference LoadAndUnload(string data)
    {
        var runtime = ModRuntime.Own(data, [_mods], ["bounty"]);
        Assert.Single(runtime.Mods.Mods);
        var loaded = AppDomain.CurrentDomain.GetAssemblies().Last(a => a.GetName().Name == "Bounty");
        var context = System.Runtime.Loader.AssemblyLoadContext.GetLoadContext(loaded);
        Assert.NotNull(context);
        Assert.True(context.IsCollectible);
        var weak = new WeakReference(context);
        runtime.Mods.Unload();
        return weak;
    }
}
