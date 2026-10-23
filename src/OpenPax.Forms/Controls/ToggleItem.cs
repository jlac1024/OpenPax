namespace OpenPax.Forms.Controls;

/// <summary>
/// A list row that is on or off (a mod turned on, say): drawn with a green
/// light when on, a red one when off, and its text in the accent color when on.
/// </summary>
public sealed record ToggleItem(string Text, bool On)
{
    public override string ToString() => Text;
}
