using OpenPax.Forms.Controls;

namespace OpenPax.Forms.Markup;

/// <summary>
/// The element names markup may use and what each makes. A mod registers
/// its own controls here under names of its own.
/// </summary>
public sealed class ControlRegistry
{
    private readonly Dictionary<string, Func<Element>> _makers = new(StringComparer.Ordinal);

    public ControlRegistry()
    {
        Register("Form", () => new Form());
        Register("StackPanel", () => new StackPanel());
        Register("Grid", () => new Grid());
        Register("DockPanel", () => new DockPanel());
        Register("Canvas", () => new CanvasPanel());
        Register("Border", () => new Border());
        Register("ScrollViewer", () => new ScrollViewer());
        Register("TextBlock", () => new TextBlock());
        Register("Button", () => new Button());
        Register("CheckBox", () => new CheckBox());
        Register("RadioButton", () => new RadioButton());
        Register("TextBox", () => new TextBox());
        Register("ListBox", () => new ListBox());
        Register("ComboBox", () => new ComboBox());
        Register("TreeView", () => new TreeView());
        Register("Slider", () => new Slider());
        Register("ProgressBar", () => new ProgressBar());
        Register("GroupBox", () => new GroupBox());
        Register("TabControl", () => new TabControl());
        Register("TabItem", () => new TabItem());
        Register("Image", () => new Image());
        Register("Separator", () => new Separator());
        Register("NumericUpDown", () => new NumericUpDown());
    }

    /// <summary>The shared registry the game's forms read with.</summary>
    public static ControlRegistry Default { get; } = new();

    public void Register(string name, Func<Element> make) => _makers[name] = make;

    public Element? Make(string name) => _makers.TryGetValue(name, out var make) ? make() : null;
}
