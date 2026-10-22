namespace OpenPax.Sim.Commands;

/// <summary>A trade package answered; the pending package it matches goes.</summary>
public sealed record TradeAnswerCommand(int Player, int Proposer, TradeTerms Offered, TradeTerms Asked, bool Accept) : Command(Player);
