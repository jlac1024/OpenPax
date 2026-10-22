namespace OpenPax.Data.Content;

/// <summary>A premade ship design human players get once they know every part of it (docs/modding/content.md).</summary>
/// <param name="Hull">Hull type 0 to 5.</param>
/// <param name="Quality">Armor class 0 to 4, as the design screen's armor buttons.</param>
/// <param name="Parts">Each part's tech id, weapon mode (firing arc 0 bow, 1 port, 2 starboard, 3 point defense; -1 for anything not a weapon) and count.</param>
public sealed record DesignContent(string Name, int Hull, int Quality, IReadOnlyList<(int Tech, int Mode, int Count)> Parts);
