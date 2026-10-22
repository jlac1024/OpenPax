namespace OpenPax.Sim;

/// <summary>What happened, in a <see cref="ModEvent"/>.</summary>
public enum ModEventKind
{
    /// <summary>A player learned a tech (research, a breakthrough, a trade, espionage or a cheat): <see cref="ModEvent.Tech"/>.</summary>
    TechLearned,

    /// <summary>A ship was finished at a shipyard, or made when nomads leave a colony: <see cref="ModEvent.Ship"/> at <see cref="ModEvent.Place"/>.</summary>
    ShipBuilt,

    /// <summary>A ship was destroyed in a battle or by a mod: <see cref="ModEvent.Ship"/>, by <see cref="ModEvent.Other"/> when known.</summary>
    ShipDestroyed,

    /// <summary>A player founded a colony on <see cref="ModEvent.Place"/>.</summary>
    ColonyFounded,

    /// <summary>A colony on <see cref="ModEvent.Place"/> went to <see cref="ModEvent.Player"/> from <see cref="ModEvent.Other"/> (a trade, a capture).</summary>
    ColonyChangedHands,

    /// <summary>A colony on <see cref="ModEvent.Place"/> was destroyed or abandoned.</summary>
    ColonyDestroyed,

    /// <summary>A treaty slot from <see cref="ModEvent.Player"/> toward <see cref="ModEvent.Other"/> changed: <see cref="ModEvent.State"/> to <see cref="ModEvent.Value"/> (Treaty's constants).</summary>
    TreatyChanged,

    /// <summary>A player was eliminated.</summary>
    PlayerEliminated,
}
