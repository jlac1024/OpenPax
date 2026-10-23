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
    private Step _step = Step.Idle;

    // The large figure open or opening (the stock one, or the designed
    // species' figure), and the small lineup figure stepped out or stepping.
    private AnimationPane? _large;
    private AnimationPane? _small;

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

    /// <summary>
    /// Where the figures are (+0x1E4). A pick or a switch made while one
    /// plays turns it around where it is, so the old figures always go back
    /// the way they came before a new one comes out.
    /// </summary>
    private enum Step
    {
        Idle,
        SmallOut,
        LargeOpening,
        Open,
        LargeClosing,
        SmallBack,
        CustomOpening,
        CustomOpen,
        CustomClosing,
    }

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
            case (>= FirstLineup and < FirstLineup + Slots) or (>= FirstLarge and <= CustomFigure):
                Finished(id);
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

    public override void Closed()
    {
        App.Sound.Stop(Audio.SoundIds.SpeciesAmbience);
    }

    /// <summary>
    /// Picks a lineup slot. With nothing out, its small figure
    /// steps out; otherwise whatever is out turns back first, and the new
    /// one follows when it is in (Finished).
    /// </summary>
    private void Select(int slot)
    {
        if (slot == _slot)
        {
            return;
        }

        if (_slot >= 0)
        {
            Window.Get(FirstHotSpot + _slot).Enabled = true;
        }

        var old = _slot;
        _slot = slot;
        switch (_step)
        {
            case Step.SmallBack when _small?.Id == FirstLineup + slot:
                // Picked again while going back: it comes out again from where it is.
                _step = Step.SmallOut;
                _small.Play(_small.Frame, false);
                break;
            case Step.Open:
                App.Sound.Play(20200 + Place(old));
                _step = Step.LargeClosing;
                Large.PlayBackward(0, false);
                ClearText();
                break;
            case Step.SmallOut:
                _step = Step.SmallBack;
                Small.PlayBackward(Small.Frame, false);
                break;
            case Step.LargeOpening:
                _step = Step.LargeClosing;
                Large.PlayBackward(Large.Frame, false);
                break;
            case Step.Idle:
                _step = Step.SmallOut;
                _small = Window.Get<AnimationPane>(FirstLineup + slot);
                _small.Play(0, false);
                // The original sounds the slot that was picked before (the first, at the start).
                App.Sound.Play(20000 + Place(Math.Max(old, 0)));
                break;
        }
    }

    /// <summary>A figure has played to its end: the next step.</summary>
    private void Finished(int id)
    {
        switch (_step)
        {
            case Step.LargeOpening:
                App.Sound.Play(20100);
                _step = Step.Open;
                ShowText();
                return;
            case Step.LargeClosing:
                _step = Step.SmallBack;
                Small.PlayBackward(0, false);
                return;
            case Step.SmallBack:
                App.Sound.Play(20300 + Place(id - FirstLineup));
                if (_custom)
                {
                    _step = Step.Idle;
                    ShowCustomFigure(true);
                    return;
                }

                _step = Step.SmallOut;
                _small = Window.Get<AnimationPane>(FirstLineup + _slot);
                _small.Play(0, false);
                App.Sound.Play(20000 + Place(_slot));
                return;
            case Step.SmallOut:
                HideLarge();
                Window.Get(FirstHotSpot + _slot).Enabled = false;
                OpenLarge(FirstLarge + _slot, Step.LargeOpening);
                return;
        }

        if (_step == Step.CustomOpening && _custom)
        {
            App.Sound.Play(20100);
            _step = Step.CustomOpen;
            ShowCustom();
        }

        if (_step == Step.CustomClosing)
        {
            if (!_custom)
            {
                ShowCustomFigure(false);
                return;
            }

            // A designed species was deleted: the figure comes back up for the next.
            HideLarge();
            OpenLarge(CustomFigure, Step.CustomOpening);
        }
    }

    /// <summary>
    /// The designed species or the lineup: the figures out go
    /// back first, then the designed species' figure comes up, or the
    /// lineup returns.
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
        // Back in the lineup every hot spot is on, the picked one's too, as in the original.
        for (var i = 0; i < Slots; i++)
        {
            Window.Get(FirstHotSpot + i).Enabled = !on;
        }

        Window.Get(Delete).Enabled = on;
        if (on)
        {
            switch (_step)
            {
                case Step.Open:
                    _step = Step.LargeClosing;
                    Large.PlayBackward(0, false);
                    break;
                case Step.LargeOpening:
                    _step = Step.LargeClosing;
                    Large.PlayBackward(Large.Frame, false);
                    break;
                case Step.SmallOut:
                    _step = Step.SmallBack;
                    Small.PlayBackward(Small.Frame, false);
                    break;
                case Step.Idle:
                    ShowCustomFigure(true);
                    break;
            }

            return;
        }

        if (_step == Step.CustomOpen)
        {
            Large.PlayBackward(0, false);
        }
        else if (_step == Step.CustomOpening)
        {
            Large.PlayBackward(Large.Frame, false);
        }

        _step = Step.CustomClosing;
    }

    /// <summary>
    /// The designed species' figure comes up in place of the lineup, or
    /// the lineup comes back with the picked figure stepping out.
    /// </summary>
    private void ShowCustomFigure(bool on)
    {
        if (on)
        {
            for (var i = 0; i < Slots; i++)
            {
                var lineup = Window.Get(FirstLineup + i);
                (lineup.Enabled, lineup.Visible) = (false, false);
            }

            HideLarge();
            OpenLarge(CustomFigure, Step.CustomOpening);
            return;
        }

        _custom = false;
        for (var i = 0; i < Slots; i++)
        {
            var lineup = Window.Get(FirstLineup + i);
            (lineup.Enabled, lineup.Visible) = (true, true);
        }

        ClearText();
        if (_small is null)
        {
            _step = Step.Idle;
        }
        else
        {
            _small.Play(0, false);
            _step = Step.SmallOut;
        }

        HideLarge();
    }

    private void OpenLarge(int id, Step step)
    {
        _large = Window.Get<AnimationPane>(id);
        (_large.Enabled, _large.Visible) = (true, true);
        _step = step;
        _large.Play(0, false);
    }

    private void HideLarge()
    {
        if (_large is not null)
        {
            (_large.Visible, _large.Enabled) = (false, false);
        }
    }

    // Each step that uses these comes after the step that set them.
    private AnimationPane Large => _large ?? throw new InvalidOperationException("no large figure out");

    private AnimationPane Small => _small ?? throw new InvalidOperationException("no lineup figure out");

    /// <summary>A slot's place in the lineup for its sounds: far left, left, middle, right, far right.</summary>
    private static int Place(int slot) => slot - (slot > 0 ? 1 : 0) - (slot > 3 ? 1 : 0) - (slot > 6 ? 1 : 0);

    /// <summary>The stock slot picked, 0 to 7.</summary>
    private void ShowCustom()
    {
        var species = App.CustomSpecies[_customIndex];
        Window.Get<TextLinePane>(NameLine).Text = species.Name;
        Window.Get<WrappedTextPane>(Description).Text = species.Description;
    }

    /// <summary>
    /// DELETE: the designed species shown goes for good, and
    /// its figure goes down and comes up again for the next; with none
    /// left, back to the lineup.
    /// </summary>
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

        if (_step == Step.CustomOpen)
        {
            _step = Step.CustomClosing;
            Large.PlayBackward(0, false);
        }
        else if (_step == Step.CustomOpening)
        {
            _step = Step.CustomClosing;
            Large.PlayBackward(Large.Frame, false);
        }
    }

    private void ClearText()
    {
        Window.Get<TextLinePane>(NameLine).Text = "";
        Window.Get<WrappedTextPane>(Description).Text = "";
    }

    private void ShowText()
    {
        var species = App.StockSpecies[_slot];
        Window.Get<TextLinePane>(NameLine).Text = species.Name;
        Window.Get<WrappedTextPane>(Description).Text = species.Description;
    }
}
