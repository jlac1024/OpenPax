namespace OpenPax.Render;

/// <summary>A black ring round text so it reads over any background (decisions.md, "Outlined text").</summary>
public enum TextOutline
{
    /// <summary>As the game draws it.</summary>
    None,

    /// <summary>The ring, the text keeping the game's colors (which mark picked, disabled and good or bad).</summary>
    KeepColors,

    /// <summary>The ring, the text white.</summary>
    White,
}
