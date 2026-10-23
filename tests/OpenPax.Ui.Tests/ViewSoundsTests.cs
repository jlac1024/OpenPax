using OpenPax.Audio;
using OpenPax.Data.Resources;
using OpenPax.Data.Views;

namespace OpenPax.Ui.Tests;

/// <summary>
/// Every sound a screen's controls play on press has a file. A release sound
/// (id + 100) may have none, as in the original's table (22500, the species
/// arrows').
/// </summary>
public sealed class ViewSoundsTests
{
    [Fact]
    public void EverySoundTheScreensAskForHasAFile()
    {
        var assets = new UiAssets(ImportedData.RequireFolder());
        var missing = new SortedSet<int>();
        foreach (var entry in assets.Views.Entries(ResourceType.View))
        {
            Walk(ViewReader.Parse(assets.Views.Get(ResourceType.View, entry.Id).Span), missing);
        }

        Assert.Empty(missing);
    }

    private static void Walk(ViewNode node, SortedSet<int> missing)
    {
        var sound = node.Details switch
        {
            ButtonDetails b => b.Sound,
            CheckBoxDetails c => c.Sound,
            StateBoxDetails s => s.Sound,
            EmpireToggleDetails e => e.Sound,
            HotSpotDetails h => h.Sound,
            SpinnerDetails s => s.Sound,
            _ => 0,
        };
        if (sound != 0)
        {
            if (SoundIds.File(sound) is null)
            {
                missing.Add(sound);
            }
        }

        foreach (var child in node.Children)
        {
            Walk(child, missing);
        }
    }
}
