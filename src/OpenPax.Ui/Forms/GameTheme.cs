using OpenPax.Data.Resources;
using OpenPax.Data.Views;
using OpenPax.Forms.Drawing;
using OpenPax.Render.TrueColor;

namespace OpenPax.Ui.Forms;

/// <summary>
/// The forms' look taken from the game: skins cut from the original's
/// network windows (VIEW 44300's art: its clamped field strip, and its
/// framed list well for panels), the Sector Display's starfield (Universe
/// 1, at 2x) behind whole forms, their buttons, arrows and scroll bar, the Game
/// Options check box and Player Design's indicator light, and colors
/// from palette 5 as those windows use them (docs/ui/forms.md).
/// </summary>
public static class GameTheme
{
    public const int Palette = 5;

    public static Theme Build(UiAssets assets)
    {
        var art = new GameArt(assets.Files);
        var palette = assets.Palette(Palette);
        Color Index(int i) => Color.Of(palette.Colors[i]);
        var back = art.Need("Main:44300");
        var green = art.Need("Main:32133");
        var heading = ViewReader.Parse(assets.Views.Get(ResourceType.View, 44300).Span).Find(44315)?.Details is LabelDetails label ? label.Style.Color : 18;
        return new Theme
        {
            Fonts = i => assets.Fonts.ForIndex(i),
            Art = art,
            DefaultFont = 1,
            Text = Index(255),
            DimText = Index(9),
            DisabledText = Index(10),
            Heading = Index(heading),
            ButtonText = Index(17),
            ButtonPressedText = Index(18),
            Accent = green.At(green.Width / 2, green.Height / 2),
            Selection = green.At(green.Width / 2, green.Height / 2).WithAlpha(90),
            Light = back.At(40, 160),
            Shade = back.At(44, 152),
            Backdrop = art.Get("Universe@2x:1") ?? art.Get("Universe:1"),
            // Blue black panels, a cool metal line round them and a shadow
            // under them: black on the starfield alone ran together with it.
            Panel = Color.Rgb(18, 21, 32),
            PanelFrame = Color.Rgb(128, 136, 156),
            PanelShadow = Color.Rgb(0, 0, 0, 170),
            Field = new Skin(back.Crop(43, 15, 250, 18), 8, 3, 8, 3),
            HeaderStrip = new Skin(back.Crop(43, 15, 250, 18), 8, 3, 8, 3),
            ListWell = new Skin(back.Crop(45, 38, 232, 112), 3, 3, 3, 3),
            Button = new Skin(art.Need("Main:44501"), 9, 4, 4, 4),
            ButtonPressed = new Skin(art.Need("Main:44601"), 9, 4, 4, 4),
            Tab = new Skin(art.Need("Main:44501"), 9, 4, 4, 4),
            TabPressed = new Skin(art.Need("Main:44601"), 9, 4, 4, 4),
            CheckOff = art.Get("Main:32033"),
            CheckOn = art.Get("Main:32133"),
            RadioOff = art.Get("Main:25004"),
            RadioOn = art.Get("Main:25104"),
            LightOn = art.Get("Main:25104"),
            LightOff = Red(art.Get("Main:25104")),
            ArrowLeft = art.Get("Main:44502"),
            ArrowLeftPressed = art.Get("Main:44602"),
            ArrowRight = art.Get("Main:44504"),
            ArrowRightPressed = art.Get("Main:44604"),
            SpinBox = art.Get("Main:18011") is { } box ? new Skin(box, 3, 3, 3, 3) : null,
            ArrowUp = art.Get("Main:44312"),
            ArrowDown = art.Get("Main:44311"),
            ScrollTrack = new Skin(art.Need("Main:44313"), 0, 13, 0, 13),
            ScrollThumb = new Skin(art.Need("Main:44310"), 0, 2, 0, 2),
        };
    }

    // The game has the green light lit (Player Design's indicator) but no red
    // one: the same light with its red and green swapped.
    private static RgbaImage? Red(RgbaImage? green) =>
        green is null ? null : new RgbaImage(green.Width, green.Height, [.. green.Pixels.Select(p => (p & 0xFFFF0000u) | ((p & 0xFFu) << 8) | ((p >> 8) & 0xFFu))]);
}
