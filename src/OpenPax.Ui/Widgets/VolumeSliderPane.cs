using OpenPax.Data.Views;
using OpenPax.Render;
using OpenPax.Sim.Numerics;

namespace OpenPax.Ui.Widgets;

/// <summary>
/// Class 8041, a volume slider: a bar filled up to the level, a frame and a
/// knob. Dragging sets the level from 0 to 10 and stores it in the
/// channel's preference as 12 per step.
/// </summary>
public sealed class VolumeSliderPane(ViewNode node, int left, int top, VolumeSliderDetails details) : Pane(node, left, top)
{
    private const double Margin = 12.5;
    private const double Travel = 139.0;

    private bool _tracking;

    public VolumeSliderDetails Details { get; } = details;

    public int Value { get; private set; } = details.Range.Value;

    private int Min => Details.Range.Min;

    private int Max => Details.Range.Max;

    /// <summary>The channel's preference: 1 sound effects, 2 CD music, 3 voice.</summary>
    public string PreferenceName => Details.Channel switch
    {
        1 => Preferences.SoundFxVolume,
        2 => Preferences.RedbookVolume,
        _ => Preferences.VoiceVolume,
    };

    public Preferences? Preferences { get; set; }

    /// <summary>The level from the stored volume: a step per 127 / range, plus one.</summary>
    public void Read(Preferences preferences)
    {
        Preferences = preferences;
        if (preferences.Values.TryGetValue(PreferenceName, out var stored))
        {
            Value = Math.Clamp(X87.ToInt(stored / (127.0 / (Max - Min))) + 1, Min, Max);
        }
    }

    protected internal override void MouseDown(int x, int y, int clicks)
    {
        _tracking = true;
        SetFrom(y);
    }

    protected internal override void MouseMove(int x, int y, long now)
    {
        if (_tracking)
        {
            SetFrom(y);
        }
    }

    protected internal override void MouseUp(int x, int y)
    {
        // a sample at the new level, an explosion for effects
        // and "Yes, commander" for voices; nothing for music.
        _tracking = false;
        var sample = PreferenceName switch
        {
            Ui.Preferences.SoundFxVolume => Audio.SoundIds.Explosion,
            Ui.Preferences.VoiceVolume => Audio.SoundIds.YesCommander,
            _ => 0,
        };
        if (sample != 0)
        {
            Window?.Assets.Sound.Stop(sample);
            PlaySound(sample);
        }
    }

    private void SetFrom(int screenY)
    {
        var y = Math.Clamp(screenY - Top, Margin, Height - Margin);
        var value = X87.ToInt(((Max - Min) * (Height - Margin - y) / Travel) + 0.5);
        Value = Math.Clamp(value, Min, Max);
        Preferences?.Set(PreferenceName, 127 / (Max - Min) * Value);
    }

    protected override void Draw(Framebuffer target)
    {
        var main = Assets.Pictures(ImageSet.Main);
        if (main.Get(32031) is { } empty)
        {
            target.Draw(empty, Left, Top + 12);
        }

        var (top, bottom) = (Top + 12, Top + Height - 12);
        var level = bottom - X87.ToInt((bottom - top) * ((double)Value / (Max - Min)));
        if (main.Get(32032) is { } full)
        {
            target.SetClip(Left, level, Width, bottom - level);
            target.Draw(full, Left, Top + 12);
            target.SetClip(Left, Top, Width, Height);
        }

        MapPane.DrawCentered(target, main.Get(32022), Left + (Width / 2), Top + (Height / 2));
        if (main.Get(_tracking ? 32121 : 32021) is { } knob)
        {
            target.Draw(knob, Left + (Width / 2) - 5, level);
        }
    }
}
