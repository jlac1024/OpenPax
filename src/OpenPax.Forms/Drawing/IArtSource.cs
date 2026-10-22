using OpenPax.Render.TrueColor;

namespace OpenPax.Forms.Drawing;

/// <summary>Where pictures come from by name: the game's art ("Main:44300") or a mod's own files.</summary>
public interface IArtSource
{
    RgbaImage? Get(string name);
}
