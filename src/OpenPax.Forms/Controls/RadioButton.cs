using OpenPax.Render.TrueColor;

namespace OpenPax.Forms.Controls;

/// <summary>One of a group (GroupName, on the same form) of which only one is on: the game's green indicator light.</summary>
public sealed class RadioButton : CheckBox
{
    public string GroupName { get; set; } = "";

    protected override RgbaImage? On => Theme.RadioOn ?? Theme.CheckOn;

    protected override RgbaImage? Off => Theme.RadioOff ?? Theme.CheckOff;

    protected override void Toggle()
    {
        if (IsChecked)
        {
            return;
        }

        IsChecked = true;
        foreach (var other in Form?.Descendants().OfType<RadioButton>() ?? [])
        {
            if (other != this && other.GroupName == GroupName)
            {
                other.IsChecked = false;
            }
        }
    }
}
