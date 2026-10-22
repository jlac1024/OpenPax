using System.Net;
using System.Text;
using System.Text.RegularExpressions;

namespace ApiDocs;

/// <summary>The HTML site of the modding docs: one page per Markdown file, flat, with the pages listed down the side. Nothing is fetched from elsewhere.</summary>
internal static partial class Site
{
    /// <summary>The page a Markdown file becomes: the overview (modding.md) is the index.</summary>
    public static string PageFor(string markdownPath)
    {
        var name = Path.GetFileNameWithoutExtension(markdownPath.Replace('\\', '/').Split('/')[^1]);
        return (name == "modding" ? "index" : name) + ".html";
    }

    public static void Write(string outFolder, IReadOnlyList<string> pages)
    {
        Directory.CreateDirectory(outFolder);
        var titles = pages.Select(p => (Path: p, Title: Title(File.ReadAllText(p)))).ToList();
        foreach (var (path, title) in titles)
        {
            var body = Markdown.ToHtml(File.ReadAllText(path));
            var nav = new StringBuilder();
            foreach (var (other, otherTitle) in titles)
            {
                var current = other == path ? " aria-current=\"page\"" : "";
                nav.Append("<li><a href=\"").Append(PageFor(other)).Append('"').Append(current).Append('>').Append(WebUtility.HtmlEncode(otherTitle)).Append("</a></li>\n");
            }

            var html = Template.Replace("{title}", WebUtility.HtmlEncode(title), StringComparison.Ordinal)
                .Replace("{nav}", nav.ToString(), StringComparison.Ordinal)
                .Replace("{body}", body, StringComparison.Ordinal);
            File.WriteAllText(Path.Combine(outFolder, PageFor(path)), html);
        }
    }

    private static string Title(string markdown) => FirstHeading().Match(markdown) is { Success: true } m ? m.Groups[1].Value.Replace("`", "", StringComparison.Ordinal) : "OpenPax modding";

    [GeneratedRegex(@"^# (.+)$", RegexOptions.Multiline)]
    private static partial Regex FirstHeading();

    private const string Template = """
        <!doctype html>
        <html lang="en">
        <head>
        <meta charset="utf-8">
        <meta name="viewport" content="width=device-width, initial-scale=1">
        <title>{title} - OpenPax modding</title>
        <style>
        :root { --bg: #fbfbf8; --fg: #1d1f22; --muted: #5b6068; --line: #dcdcd4; --code: #eeeee8; --link: #1f5fa8; --side: #f2f2ec; }
        @media (prefers-color-scheme: dark) { :root { --bg: #15171a; --fg: #e3e3de; --muted: #9aa0a8; --line: #2c3036; --code: #22262b; --link: #7fb0ea; --side: #1b1e22; } }
        * { box-sizing: border-box; }
        body { margin: 0; background: var(--bg); color: var(--fg); font: 16px/1.55 system-ui, sans-serif; display: flex; min-height: 100vh; }
        nav { width: 15rem; flex: none; background: var(--side); border-right: 1px solid var(--line); padding: 1.5rem 1rem; position: sticky; top: 0; height: 100vh; overflow-y: auto; }
        nav .brand { font-weight: 700; margin-bottom: 1rem; }
        nav ul { list-style: none; padding: 0; margin: 0; }
        nav li { margin: .25rem 0; }
        nav a { color: var(--fg); text-decoration: none; }
        nav a[aria-current] { color: var(--link); font-weight: 600; }
        main { flex: 1; min-width: 0; max-width: 56rem; padding: 2rem 2.5rem 4rem; }
        h1 { margin-top: 0; }
        h2 { margin-top: 2.5rem; border-bottom: 1px solid var(--line); padding-bottom: .3rem; }
        h3 { margin-top: 2rem; }
        a { color: var(--link); }
        code { background: var(--code); padding: .1em .3em; border-radius: 3px; font-size: .9em; overflow-wrap: anywhere; }
        pre { background: var(--code); padding: .8rem 1rem; border-radius: 4px; overflow-x: auto; }
        pre code { background: none; padding: 0; }
        table { border-collapse: collapse; margin: 1rem 0; display: block; overflow-x: auto; }
        th, td { border: 1px solid var(--line); padding: .35rem .6rem; text-align: left; vertical-align: top; }
        li { margin: .2rem 0; }
        @media (max-width: 760px) { body { display: block; } nav { width: auto; height: auto; position: static; border-right: none; border-bottom: 1px solid var(--line); } main { padding: 1.25rem 1rem 3rem; } }
        </style>
        </head>
        <body>
        <nav><div class="brand">OpenPax modding</div><ul>
        {nav}</ul></nav>
        <main>
        {body}
        </main>
        </body>
        </html>
        """;
}
