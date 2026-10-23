using OpenPax.Data.Import;
using OpenPax.Forms.Controls;
using OpenPax.Render.Fonts;
using OpenPax.Render.TrueColor;
using OpenPax.TestSupport;
using OpenPax.Ui.Setup;

namespace OpenPax.Ui.Tests;

/// <summary>The first-run setup (docs/first-run.md): picking the player's copy and importing it, with no game data loaded.</summary>
public sealed class SetupTests
{
    private static ModernFont? Font() => ModernFont.Load(Path.Combine(ImportedData.RequireFolder(), "fonts"));

    private static (SetupApp App, SetupModel Model, List<string> Imported) Make(IReadOnlyList<string> found, bool succeed = true)
    {
        var imported = new List<string>();
        var model = new SetupModel(found, (source, progress, log) =>
        {
            imported.Add(source);
            progress.Report(new ImportProgress(0.5, "Halfway"));
            log.WriteLine(succeed ? "fine" : "The files are not the GOG build's.");
            return succeed;
        });
        return (new SetupApp(Font(), model), model, imported);
    }

    private static void Settle(SetupApp app)
    {
        for (var i = 0; i < 200 && app.Model.Busy; i++)
        {
            Thread.Sleep(5);
            app.Tick(i * 20);
        }

        app.Tick(5000);
    }

    [Fact]
    public void AFoundCopyIsPickedAndImported()
    {
        var game = GameData.RequireFolder();
        var (app, model, imported) = Make([game]);
        Assert.Equal(game, model.Path);
        Assert.True(model.CanImport);

        model.OnImport();
        Settle(app);

        Assert.Equal([game], imported);
        Assert.True(model.Done);
        app.Draw(new Canvas(1280, 960)); // draws with no game data loaded
    }

    [Fact]
    public void AFolderThatIsNotTheGameIsTurnedAway()
    {
        var (_, model, imported) = Make([]);
        Assert.False(model.CanImport);
        model.Path = Path.GetTempPath();
        model.OnImport();
        Assert.Empty(imported);
        Assert.False(model.Busy);
        Assert.Contains("not the game's folder", model.Status, StringComparison.Ordinal);
    }

    [Fact]
    public void AFailedImportSaysWhyAndCanBeTriedAgain()
    {
        var game = GameData.RequireFolder();
        var (app, model, _) = Make([game], succeed: false);
        model.OnImport();
        Settle(app);
        Assert.False(model.Done);
        Assert.False(model.Busy);
        Assert.Contains("not the GOG build", model.Status, StringComparison.Ordinal);
        Assert.True(model.CanImport);
    }

    [Fact]
    public void ThePickersAreAskedForAndTheirAnswerTaken()
    {
        var (_, model, _) = Make([]);
        var asked = new List<string>();
        model.FolderWanted += () => asked.Add("folder");
        model.InstallerWanted += () => asked.Add("installer");
        model.OnBrowseFolder();
        model.OnBrowseInstaller();
        Assert.Equal(["folder", "installer"], asked);

        model.Picked(null);
        Assert.Equal("", model.Path);
        model.Picked("/some/setup_pax_imperia.exe");
        Assert.Equal("/some/setup_pax_imperia.exe", model.Path);
        model.OnQuit();
        Assert.True(model.Quit);
    }

    [Fact]
    public void TheGalaxyStubIsTurnedAwayWithAWarning()
    {
        var (_, model, imported) = Make([]);
        var stub = Path.Combine(Path.GetTempPath(), $"openpax-stub-{Guid.NewGuid():N}.exe");
        var bytes = new byte[2048];
        bytes[0] = (byte)'M';
        bytes[1] = (byte)'Z';
        System.Text.Encoding.Unicode.GetBytes("GOG Galaxy - Game Installer.exe").CopyTo(bytes, 300);
        File.WriteAllBytes(stub, bytes);
        try
        {
            model.Picked(stub);
            Assert.Contains("Galaxy installer", model.Status, StringComparison.Ordinal);
            model.OnImport();
            Assert.Empty(imported);
            Assert.False(model.Busy);
            Assert.Contains("offline backup installer", model.Status, StringComparison.Ordinal);
        }
        finally
        {
            File.Delete(stub);
        }
    }

    [Fact]
    public void OnlyAStubFoundSaysSo()
    {
        var model = new SetupModel([], (_, _, _) => true, galaxyStubFound: true);
        Assert.Contains("Galaxy installer", model.Status, StringComparison.Ordinal);
    }

    [Fact]
    public void TheScreenTicksBeforeTheImportFirstReports()
    {
        // The crash on Windows (0.1.0): the frame after IMPORT came before the
        // import's first progress report, whose text was still unset.
        var game = GameData.RequireFolder();
        using var go = new ManualResetEventSlim();
        var model = new SetupModel([game], (_, progress, _) =>
        {
            go.Wait(TestContext.Current.CancellationToken);
            progress.Report(new ImportProgress(0.5, "Halfway"));
            return true;
        });
        model.OnImport();
        model.Tick();
        Assert.True(model.Busy);
        Assert.Equal(0, model.Progress);

        go.Set();
        for (var i = 0; i < 400 && !model.Done; i++)
        {
            Thread.Sleep(5);
            model.Tick();
        }

        Assert.True(model.Done);
    }
}
