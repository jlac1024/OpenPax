using OpenPax.Forms;
using OpenPax.Forms.Markup;

namespace OpenPax.Ui.Forms;

/// <summary>The game's own forms, whose markup ships inside the assembly (Forms/Markup/*.xml).</summary>
public static class GameForms
{
    public static string Markup(string name)
    {
        var assembly = typeof(GameForms).Assembly;
        using var stream = assembly.GetManifestResourceStream("OpenPax.Ui.Forms.Markup." + name + ".xml")
            ?? throw new FileNotFoundException(name);
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }

    public static Form Read(string name, object? codeBehind = null) => new FormReader().Read(Markup(name), codeBehind);
}
