using System.Globalization;
using OpenPax.Audio;
using OpenPax.Data.Tables;
using OpenPax.Data.Views;
using OpenPax.Ui.Widgets;

namespace OpenPax.Ui.Screens;

/// <summary>
/// Player Design (mode 9, VIEW 11300, palette 4): a species of the player's
/// own, started from the stock or designed species picked in Species
/// Selection. Atmospheres, physical and cultural traits, rates and research
/// levels cost points from a budget of 10; Accept keeps the species with the
/// designed ones and goes on to Game Setup with it.
/// </summary>
public sealed class PlayerDesignScreen : Screen
{
    public const int ViewId = 11300;
    public const int PaletteIndex = 4;

    private const int Cancel = 11301;
    private const int Clear = 11302;
    private const int Accept = 11303;
    private const int PreviousPicture = 11304;
    private const int NextPicture = 11305;
    private const int FirstAtmosphere = 11306;
    private const int FirstPhysical = 11310;
    private const int FirstCultural = 11315;
    private const int FirstRate = 11322;
    private const int FirstLevel = 11327;
    private const int FirstTemperature = 11332;
    private const int NameEdit = 11341;
    private const int DescriptionEdit = 11342;
    private const int PointsSpent = 11343;
    private const int PointsLeft = 11344;
    private const int FirstLevelValue = 11345;
    private const int Help = 11350;
    private const int Portrait = 11351;
    private const int Marker = 11352;
    private const int CostCaption = 11418;
    private const int FirstAtmosphereCost = 11434;
    private const int FirstRateValue = 11438;
    private const int HelpList = 3500;
    private const int HelpEntries = 49;
    private const int HelpDelay = 350;
    private const int RateNames = 11302;
    private const int LevelNames = 11301;
    private const int Pictures = 18;
    private const byte CostHighlight = 18;
    private const byte CostPlain = 56;

    private static readonly int[] _temperatures = [160, 130, 100, 70, 40, 10];

    private readonly bool[] _atmospheres = new bool[4];
    private readonly bool[] _physical = new bool[5];
    private readonly bool[] _cultural = new bool[7];
    private readonly sbyte[] _rates = new sbyte[5];
    private readonly sbyte[] _levels = new sbyte[5];
    private int _raceId;
    private int _picture;
    private int _temperature;
    private int _tolerance;
    private int _hoverId;
    private long _helpShown;

    public PlayerDesignScreen(UiApp app)
        : base(app, new ViewWindow(app.Assets, ViewId, PaletteIndex, 25000))
    {
        app.Assets.Cursor.Show(MouseCursor.PlayerDesign);
        for (var i = 0; i < 5; i++)
        {
            Window.Get<SpinnerPane>(FirstRate + i).NamesList = RateNames;
            Window.Get<SpinnerPane>(FirstLevel + i).NamesList = LevelNames;
        }

        // the template's every field, then the name "Unnamed".
        var template = app.DesignTemplate ?? Defaults();
        Take(template with { Name = DefaultName });
        Refresh();
        _ = Window.TakeMessages().ToList();
    }

    private string DefaultName => App.Assets.Strings.Get(new TextRef(RaceTemplate.DefaultNameString.List, RaceTemplate.DefaultNameString.Index));

    private RaceTemplate Defaults() => RaceTemplate.Defaults(DefaultName,
        App.Assets.Strings.Get(new TextRef(RaceTemplate.DefaultDescriptionString.List, RaceTemplate.DefaultDescriptionString.Index)));

    /// <summary>The species as designed so far.</summary>
    public RaceTemplate Species => new(
        _raceId, Window.Get<TextEditPane>(NameEdit).Text, Window.Get<TextEditPane>(DescriptionEdit).Text, _picture, _temperature, _tolerance,
        [.. _atmospheres], [.. _physical], [.. _cultural], [.. _rates], [.. _levels]);

    /// <summary>Puts a species into the controls.</summary>
    private void Take(RaceTemplate species)
    {
        (_raceId, _picture, _temperature, _tolerance) = (species.RaceId, species.Picture, species.PreferredTemperature, species.TemperatureTolerance);
        Copy(species.Atmospheres, _atmospheres, FirstAtmosphere);
        Copy(species.PhysicalTraits, _physical, FirstPhysical);
        Copy(species.CulturalTraits, _cultural, FirstCultural);
        for (var i = 0; i < 5; i++)
        {
            _rates[i] = species.RateModifiers[i];
            _levels[i] = species.ResearchLevels[i];
            Window.Get<SpinnerPane>(FirstRate + i).Value = _rates[i];
            Window.Get<SpinnerPane>(FirstLevel + i).Value = _levels[i];
        }

        LatchTemperature();
        Window.Get<TextEditPane>(NameEdit).Text = species.Name;
        Window.Get<TextEditPane>(DescriptionEdit).Text = species.Description;
    }

    private void Copy(IReadOnlyList<bool> from, bool[] to, int firstBox)
    {
        for (var i = 0; i < to.Length; i++)
        {
            to[i] = from[i];
            Window.Get<CheckBoxPane>(firstBox + i).Set(to[i]);
        }
    }

    /// <summary>Only the button of the temperature held stays down.</summary>
    private void LatchTemperature()
    {
        for (var i = 0; i < _temperatures.Length; i++)
        {
            Window.Get<ButtonPane>(FirstTemperature + i).Pressed = _temperatures[i] == _temperature;
        }
    }

    public override void Handle(UiMessage message, long now)
    {
        var id = message.Message;
        switch (id)
        {
            case Cancel:
                App.Sound.Play(SoundIds.SpeciesDeclined);
                App.Switch(ScreenMode.SpeciesSelection);
                return;
            case Accept when Window.Get(Accept).Enabled:
                AcceptSpecies();
                return;
            case Clear:
                Take(Defaults());
                break;
            case PreviousPicture or NextPicture:
                _picture = (_picture + (id == NextPicture ? 1 : Pictures - 1)) % Pictures;
                break;
            case >= FirstAtmosphere and < FirstPhysical:
                _atmospheres[id - FirstAtmosphere] = Window.Get<CheckBoxPane>(id).Checked;
                KeepAnAtmosphere(id);
                break;
            case >= FirstPhysical and < FirstCultural:
                _physical[id - FirstPhysical] = Window.Get<CheckBoxPane>(id).Checked;
                break;
            case >= FirstCultural and < FirstRate:
                _cultural[id - FirstCultural] = Window.Get<CheckBoxPane>(id).Checked;
                break;
            case >= FirstTemperature and < FirstTemperature + 6:
                _temperature = _temperatures[id - FirstTemperature];
                LatchTemperature();
                break;
            case UiMessage.Within:
                ShowHelp(message.Value, now);
                break;
            case UiMessage.Left:
                ShowHelp(0, now);
                break;
            default:
                SpinnerChanged(message);
                break;
        }

        Refresh();
    }

    /// <summary>A spinner let go: its rate or research level takes the value.</summary>
    private void SpinnerChanged(UiMessage message)
    {
        for (var i = 0; i < 5; i++)
        {
            if (Window.Get<SpinnerPane>(FirstRate + i).Message == message.Message)
            {
                _rates[i] = (sbyte)message.Value;
            }

            if (Window.Get<SpinnerPane>(FirstLevel + i).Message == message.Message)
            {
                _levels[i] = (sbyte)message.Value;
            }
        }
    }

    /// <summary>
    /// Unticking the last atmosphere ticks its partner: carbon
    /// dioxide with methane and ammonia, oxygen and nitrogen with sulfur dioxide.
    /// </summary>
    private void KeepAnAtmosphere(int box)
    {
        if (Enumerable.Range(0, 4).Any(i => Window.Get<CheckBoxPane>(FirstAtmosphere + i).Checked))
        {
            return;
        }

        var partner = (box - FirstAtmosphere) ^ 1;
        _atmospheres[partner] = true;
        Window.Get<CheckBoxPane>(FirstAtmosphere + partner).Set(true);
    }

    /// <summary>
    /// ACCEPT (0x2C27): the species is kept with the designed ones and goes
    /// on to Game Setup. Quirk kept: every Accept adds another copy.
    /// </summary>
    private void AcceptSpecies()
    {
        var species = Species;
        if (App.CustomSpecies.Count < UiApp.MaxCustomSpecies)
        {
            App.CustomSpecies.Add(species);
            App.SaveCustomSpecies();
        }

        App.ChosenSpecies = species;
        App.Sound.Play(SoundIds.SpeciesComplete);
        App.Switch(ScreenMode.GameSetup);
    }

    /// <summary>The figures, the marker, the portrait and what may be used.</summary>
    private void Refresh()
    {
        var species = Species;
        var points = species.Points();
        Text(PointsSpent, points);
        Text(PointsLeft, RaceTemplate.PointBudget - points);
        for (var i = 0; i < 5; i++)
        {
            Text(FirstLevelValue + i, _levels[i]);
            Text(FirstRateValue + i, _rates[i]);
        }

        var portrait = Window.Get<PicturePane>(Portrait);
        portrait.Set = ImageSet.Advisors;
        portrait.Picture = (_picture + 1) * 1000;

        var breathes = !_physical[0];
        var surface = !_physical[1];
        var count = breathes ? _atmospheres.Count(a => a) : 0;
        for (var i = 0; i < 4; i++)
        {
            var box = Window.Get(FirstAtmosphere + i);
            box.Visible = breathes;
            box.Enabled = breathes;
            var cost = Window.Get<LabelPane>(FirstAtmosphereCost + i);
            cost.Visible = breathes;
            cost.Color = i == Math.Max(count - 1, 0) ? CostHighlight : CostPlain;
        }

        Window.Get(CostCaption).Visible = breathes;
        for (var i = 0; i < 6; i++)
        {
            var button = Window.Get(FirstTemperature + i);
            button.Visible = surface;
            button.Enabled = surface;
        }

        var marker = Window.Get(Marker);
        marker.Visible = surface;
        marker.Place(395 + (int)((_temperature - -75.0) * 0.6), marker.Top, marker.Width, marker.Height);

        Window.Get(Accept).Enabled = points <= RaceTemplate.PointBudget && species.Name.Length != 0 && species.Description.Length != 0;
    }

    private void Text(int id, int value)
    {
        Window.Get<TextLinePane>(id).Text = value.ToString(CultureInfo.InvariantCulture);
    }

    /// <summary>
    /// Hover help. Quirk kept: the wait counts from
    /// when help last showed, not from when the pointer arrived.
    /// </summary>
    private void ShowHelp(int id, long now)
    {
        var help = Window.Get<WrappedTextPane>(Help);
        if (id != _hoverId)
        {
            _hoverId = id;
            help.Text = "";
            return;
        }

        if (now - _helpShown >= HelpDelay)
        {
            _helpShown = now;
            var entry = id - Cancel;
            help.Text = entry is >= 0 and < HelpEntries ? App.Assets.Strings.Get(new TextRef(HelpList, entry)) : "";
        }
    }
}
