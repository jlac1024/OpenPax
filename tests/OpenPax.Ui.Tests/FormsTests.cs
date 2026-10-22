using OpenPax.Forms;
using OpenPax.Forms.Controls;
using OpenPax.Forms.Markup;
using OpenPax.Render;
using OpenPax.Render.TrueColor;
using OpenPax.Ui.Forms;
using static OpenPax.Ui.Tests.UiGames;

namespace OpenPax.Ui.Tests;

/// <summary>The forms toolkit with the game's theme, driven through the app as the host drives it.</summary>
public sealed class FormsTests
{
    private readonly UiApp _app = NewApp();
    private readonly Canvas _canvas = new(1280, 960);

    private long _now;

    private void Frame()
    {
        // Time moves on a little each frame, so clicks a frame apart are not a double click.
        _now += 500;
        _ = _app.Tick(_now).ToList();
        _app.DrawCanvas(_canvas, ClassicPlacement.For(1280, 960, 640, 480));
    }

    /// <summary>A click at the middle of an element, in canvas pixels as the host sends it.</summary>
    private void Click(Element e, int dx = -1, int dy = -1)
    {
        var scale = _app.Forms.Scale;
        var x = (dx < 0 ? e.Bounds.X + e.Bounds.Width / 2 : e.Bounds.X + dx) * scale;
        var y = (dy < 0 ? e.Bounds.Y + e.Bounds.Height / 2 : e.Bounds.Y + dy) * scale;
        _app.FormsMouseMove(x, y);
        Assert.True(_app.FormsMouseDown(x, y, false));
        Assert.True(_app.FormsMouseUp(x, y));
        Frame();
    }

    private (Form Form, GalleryModel Model) Gallery()
    {
        var form = GalleryModel.Show(_app);
        Frame();
        return (form, (GalleryModel)form.CodeBehind!); // Show sets the model as code-behind.
    }

    [Fact]
    public void BoundControlsShowAndChangeTheirViewModel()
    {
        var (form, model) = Gallery();
        var name = form.Descendants().OfType<TextBox>().First();
        Assert.Equal("Commander", name.Text);

        Click(name);
        _app.Key(UiKey.End, false, 0);
        _app.Typed("X", 0);
        Assert.Equal("CommanderX", model.Name);

        var animate = form.Descendants().OfType<CheckBox>().First(c => c.Text == "Play surface animation");
        Click(animate);
        Assert.False(model.Animate);

        model.Volume = 3;
        Assert.Equal(3, form.Descendants().OfType<ProgressBar>().Single().Value);
    }

    [Fact]
    public void RadioButtonsOfAGroupTakeTurns()
    {
        var (form, _) = Gallery();
        var radios = form.Descendants().OfType<RadioButton>().ToList();
        Click(radios[2]);
        Assert.Equal([false, false, true], radios.Select(r => r.IsChecked));
        Click(radios[0]);
        Assert.Equal([true, false, false], radios.Select(r => r.IsChecked));
    }

    [Fact]
    public void AComboBoxOpensItsListAndTakesAPick()
    {
        var (form, model) = Gallery();
        var combo = form.Descendants().OfType<ComboBox>().Single();
        Click(combo);
        Assert.True(combo.IsDropDownOpen);
        var scale = _app.Forms.Scale;
        var row = combo.Bounds.Bottom + 3 + _app.Assets.Fonts.ForIndex(1).LineHeight + 4;
        var x = (combo.Bounds.X + 20) * scale;
        _app.FormsMouseMove(x, row * scale);
        _app.FormsMouseDown(x, row * scale, false);
        _app.FormsMouseUp(x, row * scale);
        Frame();
        Assert.False(combo.IsDropDownOpen);
        Assert.Equal(1, model.SpeciesIndex);
    }

    [Fact]
    public void EnteringATextBoxSelectsAllSoTypingReplacesIt()
    {
        var (form, model) = Gallery();
        var name = form.Descendants().OfType<TextBox>().First();
        Click(name);
        Assert.True(name.AllSelected);
        _app.Typed("Jeff", 0);
        Assert.Equal("Jeff", model.Name);
        Assert.False(name.AllSelected);

        // A second click inside places the caret rather than selecting again.
        Click(name, 4);
        Assert.False(name.AllSelected);
        Assert.Equal(0, name.Caret);

        // Tab away and back, then Backspace clears the lot.
        _app.Key(UiKey.Tab, false, 0);
        _app.Key(UiKey.Tab, true, 0);
        Assert.True(name.IsFocused);
        Assert.True(name.AllSelected);
        _app.Key(UiKey.Backspace, false, 0);
        Assert.Equal("", model.Name);
    }

    [Fact]
    public void ASpinnersArrowRepeatsWhileHeldAndItsBoxDrags()
    {
        var form = OpenPax.Ui.Forms.GameForms.Read("Options", new OpenPax.Ui.Forms.OptionsModel(_app));
        _app.Forms.Show(form);
        Frame();
        var flag = form.Get<NumericUpDown>("Flag");
        var start = flag.Value;
        var scale = _app.Forms.Scale;
        var (x, y) = ((flag.Bounds.Right - 3) * scale, (flag.Bounds.Y + (flag.Bounds.Height / 2)) * scale);
        _app.FormsMouseMove(x, y);
        _app.FormsMouseDown(x, y, false);
        Assert.Equal(start + 1, flag.Value);
        for (var t = 0; t < 600; t += 25)
        {
            _ = _app.Tick(_now + t).ToList();
        }

        Assert.True(flag.Value > start + 2, $"held: {flag.Value}");
        _app.FormsMouseUp(x, y);

        // Across the box: a step per unit.
        flag.Value = 10;
        var mid = (flag.Bounds.X + (flag.Bounds.Width / 2)) * scale;
        _app.FormsMouseMove(mid, y);
        _app.FormsMouseDown(mid, y, false);
        _app.FormsMouseMove(mid + (3 * scale), y);
        _app.FormsMouseUp(mid + (3 * scale), y);
        Assert.Equal(13, flag.Value);
    }

    [Fact]
    public void ASecondClickOnAComboBoxClosesItsList()
    {
        var (form, _) = Gallery();
        var combo = form.Descendants().OfType<ComboBox>().Single();
        Click(combo);
        Assert.True(combo.IsDropDownOpen);
        Click(combo);
        Assert.False(combo.IsDropDownOpen);
    }

    [Fact]
    public void ButtonsCallTheCodeBehindAndTabsAndTreesOpen()
    {
        var (form, model) = Gallery();
        Click(form.Descendants().OfType<Button>().First(b => b.Text == "PUSH ME"));
        Assert.Equal("Pushed 1 times", model.Status);
        Assert.Contains(form.Descendants().OfType<TextBlock>(), t => t.Text == "Pushed 1 times");

        var tree = form.Descendants().OfType<TreeView>().Single();
        var before = tree.Rows.Count;
        var fleets = tree.Rows.First(r => r.Item.Header == "Fleets");
        var line = _app.Assets.Fonts.ForIndex(1).LineHeight + 2;
        var y = tree.Bounds.Y + 3 + tree.Rows.ToList().IndexOf(fleets) * line + line / 2;
        var x = tree.Bounds.X + 3 + 2 + fleets.Depth * 10 + 3;
        _app.FormsMouseMove(x * _app.Forms.Scale, y * _app.Forms.Scale);
        _app.FormsMouseDown(x * _app.Forms.Scale, y * _app.Forms.Scale, false);
        _app.FormsMouseUp(x * _app.Forms.Scale, y * _app.Forms.Scale);
        Assert.Equal(before + 2, tree.Rows.Count);

        var tabs = form.Descendants().OfType<TabControl>().Single();
        Click(tabs, 80, 8);
        Assert.Equal(1, tabs.SelectedIndex);

        Click(form.Get<Button>("Close"));
        Assert.Null(_app.Forms.Top);
    }

    [Fact]
    public void BadMarkupSaysWhatAndWhere()
    {
        var e = Assert.Throws<MarkupException>(() => new FormReader().Read("<Form>\n  <Bogus />\n</Form>"));
        Assert.Equal(2, e.Line);
        Assert.Throws<MarkupException>(() => new FormReader().Read("<Form><Button Width=\"wide\" /></Form>"));
        Assert.Throws<MarkupException>(() => new FormReader().Read("<StackPanel />"));
    }

    [Fact]
    public void AModRegistersItsOwnControl()
    {
        var registry = new ControlRegistry();
        registry.Register("Banner", () => new TextBlock { Look = "Heading" });
        var form = new FormReader(registry).Read("<Form><Banner x:Name=\"B\" Text=\"Hi\" xmlns:x=\"x\" /></Form>");
        Assert.Equal("Hi", form.Get<TextBlock>("B").Text);
    }
}
