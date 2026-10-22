namespace ApiDocs;

/// <summary>
/// ApiDocs &lt;docs folder&gt; &lt;site folder&gt;: writes the API reference into
/// docs/modding/api.md (only when it changed, so an unchanged build leaves
/// the file alone), then the HTML site of docs/modding.md and every page in
/// docs/modding/ into the site folder.
/// </summary>
internal static class Program
{
    // The guides in reading order; any other page in the folder follows, by name.
    private static readonly string[] _order = ["getting-started", "manifest", "data-packs", "content", "content-fields", "rules", "multiplayer", "samples", "api"];

    private static int Main(string[] args)
    {
        if (args.Length != 2)
        {
            Console.Error.WriteLine("usage: ApiDocs <docs folder> <site folder>");
            return 2;
        }

        var (docs, site) = (args[0], args[1]);
        var folder = Path.Combine(docs, "modding");
        Directory.CreateDirectory(folder);
        var api = Path.Combine(folder, "api.md");
        Keep(api, Reference.Write(Console.Error));
        Keep(Path.Combine(folder, "content-fields.md"), ContentFields.Write());

        var pages = Directory.GetFiles(folder, "*.md")
            .OrderBy(p => Array.IndexOf(_order, Path.GetFileNameWithoutExtension(p)) is var k && k >= 0 ? k : _order.Length)
            .ThenBy(p => p, StringComparer.Ordinal)
            .Prepend(Path.Combine(docs, "modding.md"))
            .Where(File.Exists)
            .ToList();
        Site.Write(site, pages);
        Console.WriteLine("ApiDocs: " + pages.Count + " pages in " + site);
        return 0;
    }

    /// <summary>A written page, only when it changed, so an unchanged build leaves the file alone.</summary>
    private static void Keep(string path, string text)
    {
        if (!File.Exists(path) || File.ReadAllText(path) != text)
        {
            File.WriteAllText(path, text);
            Console.WriteLine("ApiDocs: " + path + " written");
        }
    }
}
