namespace OpenPax.Sim.Commands;

/// <summary>A trade package offered.</summary>
public sealed record TradeOfferCommand(int Player, int Target, TradeTerms Offered, TradeTerms Asked) : Command(Player);
