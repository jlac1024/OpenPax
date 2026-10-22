using OpenPax.Data.Tables.Tech;

namespace OpenPax.Data.Content;

/// <summary>A tech a content pack adds or changes (docs/modding/content.md).</summary>
/// <param name="Id">The tech's id: its own for a changed one, the next free one (after the game's and earlier packs') for a new one.</param>
/// <param name="Key">A new tech's name in content files, which other entries use to refer to it; null for a changed one.</param>
/// <param name="Name">Its name as the screens show it, or null to keep the original's.</param>
/// <param name="Description">Its description, or null to keep the original's.</param>
/// <param name="Look">The original tech whose place-based looks and roles it takes (a fighter's art, a colonizer's generation): its "like", else itself.</param>
public sealed record TechContent(int Id, string? Key, TechRecord Record, string? Name, string? Description, int Look);
