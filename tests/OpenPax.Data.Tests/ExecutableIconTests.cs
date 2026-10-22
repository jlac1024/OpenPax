using OpenPax.Data.Install;
using OpenPax.TestSupport;

namespace OpenPax.Data.Tests;

public sealed class ExecutableIconTests
{
    [Fact]
    public void TheGamesIconIsThirtyTwoSquareInSixteenColors()
    {
        var exe = File.ReadAllBytes(Path.Combine(GameData.RequireFolder(), GameInstall.Executable));
        var icon = ExecutableIcon.Read(exe);

        Assert.NotNull(icon);
        Assert.Equal((32, 32), (icon.Width, icon.Height));
        Assert.Equal(17, icon.Palette.Count); // sixteen colors and the see-through one, which the GOG build's mask never uses
        Assert.True(icon.Pixels.Distinct().Count() > 2);
    }

    [Fact]
    public void AFileThatIsNoExecutableHasNoIcon() => Assert.Null(ExecutableIcon.Read([1, 2, 3]));
}
