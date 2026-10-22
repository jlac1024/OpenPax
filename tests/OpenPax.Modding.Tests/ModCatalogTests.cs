namespace OpenPax.Modding.Tests;

public sealed class ModCatalogTests
{
    [Fact]
    public void AManifestReadsItsFields()
    {
        var m = ModManifest.Parse("id = \"pack\"\nname = \"Pack\"\nversion = \"2.1\"\ndescription = \"Art.\"\n");
        Assert.Equal("pack", m.Id);
        Assert.Equal("Pack", m.Name);
        Assert.Equal("pack@2.1", m.Key);
        Assert.Null(m.Assembly);
    }

    [Fact]
    public void ModsAreFoundAndLoadedInTheOrderTurnedOn()
    {
        using var temp = new TempFolder();
        temp.Write("one/mod.toml", "id = \"one\"\nname = \"One\"\nversion = \"1\"\n");
        temp.Write("one/data/art/x.png", "one");
        temp.Write("two/mod.toml", "id = \"two\"\nname = \"Two\"\nversion = \"3\"\n");
        temp.Write("two/data/art/x.png", "two");
        temp.Write("zcopy/mod.toml", "id = \"one\"\nname = \"Again\"\nversion = \"1\"\n");

        var host = ModHost.Load(temp.Path, ["two", "one", "missing"]);

        Assert.Equal(["two@3", "one@1"], host.Fingerprint.Split(',').Select(k => k.Split('#')[0])); // each with its files' hash after the #
        Assert.Equal(2, host.Errors.Count); // the duplicate id and the missing mod
        Assert.Contains(host.Errors, e => e.Contains("missing", StringComparison.Ordinal));
        var files = host.Data(Path.Combine(temp.Path, "base"));
        Assert.Equal("one", File.ReadAllText(files.Path("art", "x.png"))); // the last mod wins
    }

    [Fact]
    public void TheUsersCopyOfAShippedModWins()
    {
        using var temp = new TempFolder();
        temp.Write("user/pack/mod.toml", "id = \"pack\"\nname = \"Mine\"\nversion = \"2\"\n");
        temp.Write("game/pack/mod.toml", "id = \"pack\"\nname = \"Shipped\"\nversion = \"1\"\n");
        temp.Write("game/other/mod.toml", "id = \"other\"\nname = \"Other\"\nversion = \"1\"\n");
        var errors = new List<string>();

        var found = ModCatalog.Find([Path.Combine(temp.Path, "user"), Path.Combine(temp.Path, "game")], errors);

        Assert.Equal(["Mine", "Other"], found.Select(m => m.Manifest.Name));
        Assert.Empty(errors);
    }

    [Fact]
    public void AnAssemblyThatCannotLoadLeavesItsModOut()
    {
        using var temp = new TempFolder();
        temp.Write("bad/mod.toml", "id = \"bad\"\nname = \"Bad\"\nversion = \"1\"\nassembly = \"Bad.dll\"\n");
        temp.Write("bad/Bad.dll", "not an assembly");

        var host = ModHost.Load(temp.Path, ["bad"]);

        Assert.Empty(host.Mods);
        Assert.Single(host.Errors);
    }
}
