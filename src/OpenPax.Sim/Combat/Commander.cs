namespace OpenPax.Sim.Combat;

/// <summary>A player's side in a battle.</summary>
public sealed class Commander(int player, bool computer)
{
    public int Player { get; } = player;

    /// <summary>Fights by itself (+0x80): a computer player, or a human side with AUTO on.</summary>
    public bool Computer { get; internal set; } = computer;

    /// <summary>Gave up the battle and is leaving (+0x81); it never thinks again.</summary>
    public bool Done { get; internal set; }

    /// <summary>Kills to its credit (+0xC): a tenth of each ship's full structure; shared out as experience at the end.</summary>
    public int Kills { get; internal set; }

    /// <summary>Think counter (+0x78): a side acts from its eighth think on; when it is odd, ships roll for retreat.</summary>
    public int Counter { get; internal set; }

    /// <summary>Its ships' full structure and structure when the battle began (+0x10, +0x14), and their structure at its last think (+0x18). Nothing reads them.</summary>
    internal int StartMaxStructure { get; set; }

    internal int StartStructure { get; set; }

    internal int StructureNow { get; set; }

    /// <summary>
    /// The strength of its first ship still on the field, when the battle
    /// began (+0x1C) and at its last think (+0x20). Sides measure how they
    /// and their enemies are faring by these when deciding to retreat.
    /// </summary>
    internal int StartStrength { get; set; }

    internal int Strength { get; set; }
}
