namespace OpenPax.Ui;

/// <summary>
/// Where checking for and installing updates stands (docs/updates.md). The
/// host writes it each frame from its own work, the screens read it.
/// </summary>
public sealed class UpdateState
{
    /// <summary>A newer release the player can update to, as "0.1.3"; null when there is none (or none known yet).</summary>
    public string? Available { get; set; }

    /// <summary>The running version, for the screens to show.</summary>
    public string Running { get; set; } = "";

    /// <summary>True while the host checks or downloads.</summary>
    public bool Busy { get; set; }

    /// <summary>How far a download has got, 0 to 1.</summary>
    public double Progress { get; set; }

    /// <summary>What the player should read about it now: "Checking...", "You have the newest", why it failed.</summary>
    public string Status { get; set; } = "";

    /// <summary>Set once the main menu has offered <see cref="Available"/>, so it asks only once a run.</summary>
    public bool Offered { get; set; }

    /// <summary>A newer version was installed outside the game while it ran (a package's updates); the main menu offers to restart into it.</summary>
    public bool InstalledOutside { get; set; }

    /// <summary>Set once the main menu has offered that restart.</summary>
    public bool RestartOffered { get; set; }
}
