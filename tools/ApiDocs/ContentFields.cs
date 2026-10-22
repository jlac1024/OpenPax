using System.Text;
using OpenPax.Data.Content;
using OpenPax.Data.Tables;
using OpenPax.Data.Tables.Tech;

namespace ApiDocs;

/// <summary>
/// The content reference (docs/modding/content-fields.md): every field a
/// content file can give a tech, by class, and a hull, from the records the
/// game reads them into and those records' doc comments.
/// </summary>
internal static class ContentFields
{
    public static string Write()
    {
        var docs = new XmlDocs([typeof(ContentPack).Assembly]);
        var md = new StringBuilder();
        md.Append("# Content fields\n\n");
        md.Append("Written by `tools/ApiDocs` from the game's own records each build; do not edit by hand. ");
        md.Append("How content files work is in [content](content.md). Every tech has the shared fields, then those of its class. ");
        md.Append("Names are in snake case; a list is written `[a, b, c]`.\n\n");

        Fields(md, "Every tech", typeof(TechBase), docs, ["tech_id", "unused13", "unused14"]);
        foreach (var (name, id) in ContentPack.Classes.OrderBy(c => c.Value))
        {
            Fields(md, $"`class = \"{name}\"` ({id})", ContentPack.DetailsOf(id), docs, []);
        }

        Fields(md, "Hulls (`[[hull]]`)", typeof(Hull), docs, ["id"]);
        return md.ToString();
    }

    private static void Fields(StringBuilder md, string title, Type record, XmlDocs docs, string[] hidden)
    {
        md.Append("## ").Append(title).Append("\n\n");
        var doc = docs.Of(XmlDocs.IdOf(record));
        var summary = XmlDocs.Text(doc?.Element("summary"));
        if (summary.Length > 0)
        {
            md.Append(summary).Append("\n\n");
        }

        var parameters = doc?.Elements("param").ToDictionary(p => RecordFields.Snake(p.Attribute("name")?.Value ?? ""), XmlDocs.Text) ?? [];
        md.Append("| Field | Type | Meaning |\n|---|---|---|\n");
        foreach (var (name, type) in RecordFields.Of(record).Where(f => !hidden.Contains(f.Name)))
        {
            md.Append("| `").Append(name).Append("` | ").Append(RecordFields.Describe(type)).Append(" | ")
                .Append(parameters.GetValueOrDefault(name, "").Replace("\n\n", " ", StringComparison.Ordinal).Replace("|", "\\|", StringComparison.Ordinal)).Append(" |\n");
        }

        md.Append('\n');
    }
}
