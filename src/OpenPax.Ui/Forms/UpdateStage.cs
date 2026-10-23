namespace OpenPax.Ui.Forms;

/// <summary>Where the update dialog is: offering the update, showing its download, or saying how it ended.</summary>
internal enum UpdateStage
{
    Offer,
    Working,
    Done,
}
