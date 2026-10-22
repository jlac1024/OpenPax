namespace OpenPax.Sim.Setup;

/// <summary>A premade ship design a mod adds (docs/modding/content.md): human players get it once they know every part of it.</summary>
/// <param name="Hull">Hull type 0 to 5.</param>
/// <param name="Quality">Armor class 0 to 4.</param>
/// <param name="Parts">Each part's tech id, weapon mode (-1 for anything not a weapon) and count.</param>
public sealed record DesignTemplate(string Name, int Hull, int Quality, IReadOnlyList<(int Tech, int Mode, int Count)> Parts);
