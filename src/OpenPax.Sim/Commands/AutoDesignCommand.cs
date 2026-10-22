namespace OpenPax.Sim.Commands;

/// <summary>AUTO: a design built for the hull becomes the player's draft. It draws from the game's random numbers.</summary>
public sealed record AutoDesignCommand(int Player, int Hull, string Name) : Command(Player);
