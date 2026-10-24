namespace OpenPax.Ui;

/// <summary>One entry of the open source and attributions window: what it is, what for, its license and that license's text.</summary>
public sealed record Attribution(string Name, string Use, string License, string Home, string Text)
{
    /// <summary>The window's entries, from the list embedded in the program (Attributions/attributions.toml), in its order.</summary>
    public static IReadOnlyList<Attribution> All { get; } = Load();

    private static List<Attribution> Load()
    {
        var table = OpenPax.Data.Toml.TomlReader.Parse(Read("attributions.toml"));
        var entries = table.TryGetValue("component", out var list) && list is System.Collections.IEnumerable items ? items.OfType<IReadOnlyDictionary<string, object>>() : [];
        string Field(IReadOnlyDictionary<string, object> entry, string key) => entry.TryGetValue(key, out var v) && v is string s ? s : "";
        return [.. entries.Select(e => new Attribution(
            Field(e, "name"),
            Field(e, "use"),
            Field(e, "license"),
            Field(e, "home"),
            Field(e, "text") is { Length: > 0 } file ? Read(file).Replace("\r\n", "\n", StringComparison.Ordinal) : ""))];
    }

    private static string Read(string name)
    {
        using var stream = typeof(Attribution).Assembly.GetManifestResourceStream("Attributions/" + name)
            ?? throw new FileNotFoundException("Attributions/" + name);
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }
}
