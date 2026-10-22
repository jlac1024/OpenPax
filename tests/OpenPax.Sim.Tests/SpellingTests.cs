using OpenPax.Sim.Saving;

namespace OpenPax.Sim.Tests;

/// <summary>Names in saves written before the move to American spelling still find their fields.</summary>
public sealed class SpellingTests
{
    [Theory]
    [InlineData("<FleetColour>k__BackingField", "<FleetColor>k__BackingField")]
    [InlineData("OpenPax.Sim.Galaxy.StarCentre", "OpenPax.Sim.Galaxy.StarCenter")]
    [InlineData("_defence", "_defense")]
    [InlineData("Treaty", "Treaty")]
    [InlineData("<Index>k__BackingField", "<Index>k__BackingField")]
    public void OldNamesTakeTheNewSpelling(string old, string now) => Assert.Equal(now, Spelling.American(old));

    [Fact]
    public void ASaveFromBeforeTheRenameFindsTheOpenPaxTypes()
    {
        // Saves made while the project was called Heliograph name its types so.
        Assert.Equal("OpenPax.Sim.Players.Player", Spelling.Current("Heliograph.Sim.Players.Player"));
        Assert.Equal("System.Collections.Generic.List<OpenPax.Sim.Players.Fleet>", Spelling.Current("System.Collections.Generic.List<Heliograph.Sim.Players.Fleet>"));
        Assert.Equal("OpenPax.Sim.Players.Armor", Spelling.Current("Heliograph.Sim.Players.Armour"));
        Assert.Equal("OpenPax.Ui.SavedSession", Spelling.Current("OpenPax.Ui.SavedSession"));
    }
}
