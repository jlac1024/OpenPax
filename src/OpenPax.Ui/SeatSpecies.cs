using OpenPax.Data.Tables;
using OpenPax.Data.Views;
using OpenPax.Net.Lobby;

namespace OpenPax.Ui;

/// <summary>
/// The species a lobby seat plays: a designed one sent whole with the
/// choice, else the stock species picked, else the stock species in turn
/// for the seat. The host and a dedicated server both make games from it.
/// </summary>
public static class SeatSpecies
{
    /// <summary>A designed species as a seat choice carries it.</summary>
    public static byte[] Pack(RaceTemplate species) => RaceTemplate.WriteSpeciesFile([species]);

    /// <summary>A designed species read back from a seat choice, or null when there is none or it does not read.</summary>
    public static RaceTemplate? Unpack(byte[]? custom, StringTable strings)
    {
        if (custom is not { Length: > 0 })
        {
            return null;
        }

        try
        {
            return RaceTemplate.ReadSpeciesFile(
                custom,
                strings.Get(new TextRef(RaceTemplate.DefaultNameString.List, RaceTemplate.DefaultNameString.Index)),
                strings.Get(new TextRef(RaceTemplate.DefaultDescriptionString.List, RaceTemplate.DefaultDescriptionString.Index))).FirstOrDefault();
        }
        catch (Exception e) when (e is Data.Resources.ResourceFormatException or InvalidDataException or ArgumentException or IndexOutOfRangeException or EndOfStreamException)
        {
            // A damaged or hostile choice: the seat gets a stock species instead.
            return null;
        }
    }

    public static RaceTemplate For(SeatChoice choice, int seat, IReadOnlyList<RaceTemplate> stock, StringTable strings) =>
        Unpack(choice.Custom, strings) ?? stock[(choice.Species >= 0 ? choice.Species : seat) % stock.Count];
}
