using OpenPax.Data.Tables;
using OpenPax.Ui.Widgets;

namespace OpenPax.Ui.Screens;

/// <summary>
/// Species Selection (VIEW 11000, palette 3): the eight built-in species
/// in a lineup, the picked one large with its name and description, or
/// the player's designed species one at a time. Accept goes on to Game
/// Setup with the species shown; Design opens the designer with it.
/// </summary>
public sealed class SpeciesSelectionScreen : Screen
{
    public const int ViewId = 11000;
    public const int PaletteIndex = 3;

    private const int Accept = 11800;
    private const int Cancel = 11801;
    private const int Stock = 11802;
    private const int Custom = 11803;
    private const int FirstHotSpot = 11010;
    private const int FirstLineup = 11020;
    private const int FirstLarge = 11030;
    private const int NameLine = 11100;
    private const int Description = 11101;
    private const int Previous = 11102;
    private const int Next = 11103;
    private const int Delete = 11104;
    private const int Design = 11105;
    private const int Slots = 8;
    private const int CustomFigure = 11038;

    private int _slot = -1;
    private bool _custom;
    private int _customIndex;

    public SpeciesSelectionScreen(UiApp app)
        : base(app, new ViewWindow(app.Assets, ViewId, PaletteIndex, ViewId))
    {
        app.Assets.Cursor.Show(MouseCursor.SpeciesSelection);
        // Custom only with designed species to show.
        Window.Get<ButtonPane>(Stock).Pressed = true;
        Window.Get(Custom).Enabled = app.CustomSpecies.Count > 0;
        Window.Get(Delete).Enabled = false;
        app.Sound.Play(Audio.SoundIds.SpeciesAmbience, loop: true);
        Select(0);
    }

    public override void Closed()
    {
        App.Sound.Stop(Audio.SoundIds.SpeciesAmbience);
    }

    /// <summary>A slot's place in the lineup for its sounds: far left, left, middle, right, far right.</summary>
    private static int Place(int slot) => slot - (slot > 0 ? 1 : 0) - (slot > 3 ? 1 : 0) - (slot > 6 ? 1 : 0);

    /// <summary>The stock slot picked, 0 to 7.</summary>
    public int Slot => _slot;

    /// <summary>Showing the designed species, and which.</summary>
    public bool ShowsCustom => _custom;

    public int CustomIndex => _customIndex;

    private RaceTemplate Shown => _custom ? App.CustomSpecies[_customIndex] : App.StockSpecies[_slot];

    public override void Handle(UiMessage message, long now)
    {
        var id = message.Message;
        switch (id)
        {
            case >= FirstHotSpot and < FirstHotSpot + Slots:
                Select(id - FirstHotSpot);
                break;
            case >= FirstLineup and < FirstLineup + Slots when id - FirstLineup == _slot && !_custom:
                // The small figure has stepped forward: the large one opens.
                var large = Window.Get<AnimationPane>(FirstLarge + _slot);
                large.Visible = true;
                large.Play(0, false);
                break;
            case >= FirstLarge and < FirstLarge + Slots when id - FirstLarge == _slot && !_custom:
                // the figure locks in front.
                App.Sound.Play(20100);
                ShowText();
                break;
            case CustomFigure when _custom:
                // the designed species' figure is up.
                App.Sound.Play(20100);
                ShowCustom();
                break;
            case Previous or Next when _custom:
                // step through the designed species, wrapping, with no figure.
                var count = App.CustomSpecies.Count;
                _customIndex = (_customIndex + (id == Next ? 1 : count - 1)) % count;
                ShowCustom();
                break;
            case Previous or Next:
                // step through the lineup, wrapping.
                Select((_slot + (id == Next ? 1 : Slots - 1)) % Slots);
                break;
            case Custom when App.CustomSpecies.Count > 0:
                ShowCustomSpecies(true);
                break;
            case Stock:
                ShowCustomSpecies(false);
                break;
            case Delete when _custom:
                DeleteCustom();
                break;
            case Design when _slot >= 0:
                // the species shown is the designer's template.
                App.DesignTemplate = Shown;
                App.Switch(ScreenMode.PlayerDesign);
                break;
            case Accept when _slot >= 0:
                App.Sound.Play(Audio.SoundIds.SpeciesSelected);
                App.ChosenSpecies = Shown;
                App.Switch(ScreenMode.GameSetup);
                break;
            case Cancel:
                App.Switch(ScreenMode.GameStart);
                break;
        }
    }

    /// <summary>
    /// Picks a lineup slot: the old figure goes back, the new
    /// one's hot spot is disabled while its small figure plays, then the
    /// large figure opens and the name and description follow.
    /// </summary>
    private void Select(int slot)
    {
        if (slot == _slot)
        {
            return;
        }

        if (_slot >= 0)
        {
            // The figure open in front turns back to the lineup.
            App.Sound.Play(20200 + Place(_slot));
            Window.Get(FirstHotSpot + _slot).Enabled = true;
            Window.Get(FirstLarge + _slot).Visible = false;
            Window.Get<AnimationPane>(FirstLineup + _slot).Show(0);
        }

        _slot = slot;
        Window.Get(FirstHotSpot + slot).Enabled = false;
        Window.Get<TextLinePane>(NameLine).Text = "";
        Window.Get<WrappedTextPane>(Description).Text = "";
        Window.Get<AnimationPane>(FirstLineup + slot).Play(0, false);
        App.Sound.Play(20000 + Place(slot));
    }

    /// <summary>
    /// The designed species or the lineup: the
    /// lineup gives way to one figure for every designed species, and back.
    /// </summary>
    private void ShowCustomSpecies(bool on)
    {
        if (on == _custom)
        {
            return;
        }

        _custom = on;
        Window.Get<ButtonPane>(Stock).Pressed = !on;
        Window.Get<ButtonPane>(Custom).Pressed = on;
        Window.Get(Delete).Enabled = on;
        Window.Get<TextLinePane>(NameLine).Text = "";
        Window.Get<WrappedTextPane>(Description).Text = "";
        for (var i = 0; i < Slots; i++)
        {
            Window.Get(FirstHotSpot + i).Enabled = !on && i != _slot;
            Window.Get(FirstLineup + i).Visible = !on;
            Window.Get(FirstLarge + i).Visible = false;
        }

        var figure = Window.Get<AnimationPane>(CustomFigure);
        figure.Visible = on;
        if (on)
        {
            _customIndex = Math.Min(_customIndex, App.CustomSpecies.Count - 1);
            App.Sound.Play(20300 + Place(_slot));
            figure.Play(0, false);
        }
        else
        {
            // The lineup comes back with the picked slot stepping out again.
            var slot = _slot;
            _slot = -1;
            Select(slot);
        }
    }

    private void ShowCustom()
    {
        var species = App.CustomSpecies[_customIndex];
        Window.Get<TextLinePane>(NameLine).Text = species.Name;
        Window.Get<WrappedTextPane>(Description).Text = species.Description;
    }

    /// <summary>DELETE: the designed species shown goes for good; with none left, back to the lineup.</summary>
    private void DeleteCustom()
    {
        App.CustomSpecies.RemoveAt(_customIndex);
        App.SaveCustomSpecies();
        var count = App.CustomSpecies.Count;
        if (count == 0)
        {
            ShowCustomSpecies(false);
            Window.Get(Custom).Enabled = false;
            return;
        }

        if (_customIndex == count)
        {
            _customIndex--;
        }

        ShowCustom();
    }

    private void ShowText()
    {
        var species = App.StockSpecies[_slot];
        Window.Get<TextLinePane>(NameLine).Text = species.Name;
        Window.Get<WrappedTextPane>(Description).Text = species.Description;
    }
}
