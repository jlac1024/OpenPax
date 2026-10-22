using OpenPax.Data.Views;
using OpenPax.Render;
using OpenPax.Sim.Players;

namespace OpenPax.Ui.Widgets;

/// <summary>
/// Class 19800, the advisor for a field: the portrait from Advisors.img
/// and, unless a governor holds the place, the skill in percent (negative
/// for an advisor working for an enemy).
/// </summary>
public sealed class AdvisorPortraitPane(ViewNode node, int left, int top) : Pane(node, left, top)
{
    public Player? Player { get; set; }

    /// <summary>The field shown, 0 research to 3 shipbuilding; 5 for a system's governor.</summary>
    public int Role { get; set; }

    /// <summary>The system whose governor is shown (+0x68); none for a field's advisor.</summary>
    public int Star { get; set; } = Advisor.NoStar;

    /// <summary>Which of the portrait's sizes (+0x60): picture + (variant + 1) * 50.</summary>
    public int Variant { get; set; } = 1;

    protected override void Draw(Framebuffer target)
    {
        // the advisor of the field at the star.
        if (Player?.Advisors.FirstOrDefault(a => a.Role == Role && a.Star == Star) is not { } advisor)
        {
            return;
        }

        MapPane.DrawCentered(target, Assets.Pictures(ImageSet.Advisors).Get(advisor.Portrait + ((Variant + 1) * 50)), Left + (Width / 2), Top + (Height / 2));
        if (!advisor.IsGovernor)
        {
            var skill = ((int)Math.Floor(advisor.Value(Role) * 100.0)).ToString(System.Globalization.CultureInfo.InvariantCulture) + "%";

            // Open: the exact text position (the rect fields are not settled).
            Assets.Fonts.ForIndex(3).Draw(target, skill, Left + Width - 4, Top + 15, 5, 2, 255, 0);
        }
    }
}
