namespace OpenPax.Ui.Forms;

/// <summary>Where the update dialog is: offering the update, showing its download, saying how it ended, or offering a restart into a version installed outside the game.</summary>
internal enum UpdateStage
{
    Offer,
    Working,
    Done,

    /// <summary>A newer version was installed outside the game: restart into it?</summary>
    Restart,
}
