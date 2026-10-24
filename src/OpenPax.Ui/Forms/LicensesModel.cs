using OpenPax.Forms;
using OpenPax.Forms.Controls;
using OpenPax.Ui.Screens;

namespace OpenPax.Ui.Forms;

/// <summary>The open source and attributions window (Forms/Markup/Licenses.xml): the components on the left, the one picked on the right.</summary>
public sealed class LicensesModel(UiApp app) : Observable
{
    private int _picked;

    /// <summary>The page on the right, which starts at its top for each component picked.</summary>
    internal ScrollViewer? Page { get; set; }

    public IReadOnlyList<string> Names { get; } = [.. Attribution.All.Select(a => a.Name)];

    public int Picked
    {
        get => _picked;
        set
        {
            if (value < 0 || value >= Attribution.All.Count || !Set(ref _picked, value))
            {
                return;
            }

            Page?.ScrollToTop();
            Changed(nameof(Name));
            Changed(nameof(Use));
            Changed(nameof(License));
            Changed(nameof(Home));
            Changed(nameof(Text));
        }
    }

    private Attribution Current => Attribution.All[_picked];

    public string Name => Current.Name;

    public string Use => Current.Use;

    public string License => string.Format(System.Globalization.CultureInfo.InvariantCulture, UiText.LicensesLicense, Current.License);

    public string Home => Current.Home;

    public string Text => Current.Text;

    public void OnDone() => app.Switch(ScreenMode.GameStart);
}
