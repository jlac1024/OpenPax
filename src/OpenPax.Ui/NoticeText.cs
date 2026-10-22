using System.Globalization;
using OpenPax.Data.Views;
using OpenPax.Sim;
using OpenPax.Sim.Galaxy;
using OpenPax.Ui.Screens;

namespace OpenPax.Ui;

/// <summary>
/// Turns the game's notices into the log's text, as the player's notice
/// classes build it: the date and ": " first,
/// then the words from the STR# lists.
/// </summary>
internal static class NoticeText
{
    private const int ResearchMode = 13;
    private const int ForeignAffairsMode = 18;

    public static UiNotice Make(UiApp app, GameSession session, GameNotice notice)
    {
        string S(int list, int index) => app.Assets.Strings.Get(new TextRef(list, index));
        string Tech(int id) => S(100000 + id, 0);
        string Paren(string name) => S(104, 7) + name + S(104, 8);
        var dated = session.Date + S(107, 0);
        var other = notice.Other?.Name ?? "";
        var kind = 200 + notice.Value;

        return notice.Kind switch
        {
            NoticeKind.ModMessage => new(dated + notice.Text, 1, false, null),
            NoticeKind.ResearchCompleted => new(dated + S(107, 1) + Tech(notice.Value), 0, false, Open(ResearchMode)),
            NoticeKind.Breakthrough => new(dated + S(107, 11) + Tech(notice.Value), 0, false, Open(ResearchMode)),
            NoticeKind.ShipFinished => new(
                dated + S(107, 2) + (notice.Ship?.Name ?? "") + S(107, 5) + StarOf(session, notice.Place)?.Name + S(107, 6), 0, false, Show(notice.Place)),
            NoticeKind.ColonyLost => new(dated + (notice.Place as Planet)?.Name + " " + S(70, 3), 2, false, Show(notice.Place)),
            NoticeKind.WarChanged => new(dated + S(80, 0) + other, 2, false, Open(ForeignAffairsMode)),
            NoticeKind.FirstContact => new(dated + S(83, 4) + other, 1, false, Open(ForeignAffairsMode)),
            NoticeKind.PlayerEliminated => new(dated + other + S(70, 5), 1, false, Open(ForeignAffairsMode)),

            // Open: the mission routines' own result words ( and the
            // rest) are not read yet; the count of things hit stands in for them.
            NoticeKind.SpySucceeded => new(dated + S(kind, 0) + Count(notice) + " " + Paren(other), 0, false, Open(ForeignAffairsMode)),
            NoticeKind.SpyFailed => new(dated + notice.Count switch
            {
                0 => S(112, 22).TrimStart() + " " + Paren(other),
                1 => S(kind, 2) + " " + Paren(other),
                _ => S(kind, 1) + " " + Paren(other),
            }, 1, false, Open(ForeignAffairsMode)),
            NoticeKind.SpyStopped => new(dated + S(kind, 3) + " " + Paren(other), 2, false, Open(ForeignAffairsMode)),
            NoticeKind.SpyDamage => new(
                dated + Count(notice) + (notice.Value == 3 ? S(203, 4) : S(81, notice.Count == 1 ? 12 : 13) + S(kind, 4))
                    + (notice.Named ? " " + Paren(other) : ""), 2, false, Open(ForeignAffairsMode)),
            NoticeKind.TradeOffered => new(dated + S(82, 8) + other, 1, false, Open(ForeignAffairsMode)),
            NoticeKind.TradeFailed => new(dated + S(82, 10) + other, 1, false, Open(ForeignAffairsMode)),
            NoticeKind.TradeAccepted => new(dated + S(82, 9) + other, 0, false, Open(ForeignAffairsMode)),
            _ => new(dated + S(82, 11) + other, 1, false, Open(ForeignAffairsMode)),
        };
    }

    private static string Count(GameNotice notice)
    {
        return notice.Count.ToString(CultureInfo.InvariantCulture);
    }

    private static Star? StarOf(GameSession session, ISpaceObject? place)
    {
        return place is Planet planet ? session.Game.Universe.Stars.FirstOrDefault(s => s.Planets.Contains(planet)) : null;
    }

    /// <summary>The common action: open a screen, remembering the one left.</summary>
    private static Action<UiApp> Open(int mode)
    {
        return app => app.Switch((ScreenMode)mode, push: true);
    }

    /// <summary>A ship or colony notice's action: its system, the place selected, on the Sector Display.</summary>
    private static Action<UiApp> Show(ISpaceObject? place)
    {
        return app =>
        {
            if (app.Session is { } session && StarOf(session, place) is { } star && place is not null)
            {
                session.SetCurrentStar(star.Index);
                session.Select(place);
            }

            app.Switch(ScreenMode.SectorDisplay, push: true);
        };
    }
}
