using OpenPax.Sdl;
using OpenPax.Ui;

namespace OpenPax.Host;

/// <summary>Real keys onto the game's key codes.</summary>
internal static class KeyMap
{
    public static UiKey? ToUiKey(uint key)
    {
        return key switch
        {
            Sdl.Sdl.KeyLeft => UiKey.Left,
            Sdl.Sdl.KeyRight => UiKey.Right,
            Sdl.Sdl.KeyUp => UiKey.Up,
            Sdl.Sdl.KeyDown => UiKey.Down,
            Sdl.Sdl.KeyHome => UiKey.Home,
            Sdl.Sdl.KeyEnd => UiKey.End,
            Sdl.Sdl.KeyBackspace => UiKey.Backspace,
            Sdl.Sdl.KeyDelete => UiKey.Delete,
            Sdl.Sdl.KeyReturn or Sdl.Sdl.KeyPadEnter => UiKey.Return,
            Sdl.Sdl.KeyTab => UiKey.Tab,
            Sdl.Sdl.KeyEscape => UiKey.Escape,
            _ => null,
        };
    }

    /// <summary>
    /// The accelerator table ("PaxIIAccelerators"): function keys switch
    /// screens, F10 and "-" slow the game, F11 and "=" speed it up, F12 and
    /// "\" pause. Keys held with Ctrl or Alt are other commands.
    /// </summary>
    public static UiCommand? ToCommand(uint key, ushort mod)
    {
        if ((mod & Sdl.Sdl.ModAlt) != 0)
        {
            return key switch
            {
                Sdl.Sdl.KeyX => UiCommand.Quit,
                Sdl.Sdl.KeyN => UiCommand.NewGame,
                _ => null,
            };
        }

        if ((mod & Sdl.Sdl.ModCtrl) != 0)
        {
            return key switch
            {
                Sdl.Sdl.KeyF1 => UiCommand.QuickSave,
                Sdl.Sdl.KeyF1 + 1 => UiCommand.QuickLoad,
                Sdl.Sdl.KeyF1 + 2 => UiCommand.LoadAutoSave,
                _ => null,
            };
        }

        return key switch
        {
            Sdl.Sdl.KeyF1 => UiCommand.Bridge,
            Sdl.Sdl.KeyF1 + 1 => UiCommand.SectorDisplay,
            Sdl.Sdl.KeyF1 + 2 => UiCommand.Research,
            Sdl.Sdl.KeyF1 + 3 => UiCommand.FleetCommand,
            Sdl.Sdl.KeyF1 + 4 => UiCommand.DomesticPolicy,
            Sdl.Sdl.KeyF1 + 5 => UiCommand.ForeignAffairs,
            Sdl.Sdl.KeyF1 + 6 => UiCommand.PlanetarySurface,
            Sdl.Sdl.KeyF1 + 7 => UiCommand.ShipDesign,
            Sdl.Sdl.KeyF1 + 8 => UiCommand.Mode20,
            Sdl.Sdl.KeyF1 + 9 or Sdl.Sdl.KeyMinus => UiCommand.SlowDown,
            Sdl.Sdl.KeyF1 + 10 or Sdl.Sdl.KeyEquals => UiCommand.SpeedUp,
            Sdl.Sdl.KeyF1 + 11 or Sdl.Sdl.KeyBackslash => UiCommand.Pause,
            Sdl.Sdl.KeyEscape => UiCommand.GameOptions,
            Sdl.Sdl.KeyGrave => UiCommand.Console,
            _ => null,
        };
    }
}
