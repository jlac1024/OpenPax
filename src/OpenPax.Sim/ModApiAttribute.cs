namespace OpenPax.Sim;

/// <summary>
/// Marks a member made for mods (docs/modding/api.md): the API reference
/// lists these first, as the rules a mod is meant to use, before the rest of
/// what the game makes public for its own screens and tools.
/// </summary>
[AttributeUsage(AttributeTargets.Method | AttributeTargets.Property | AttributeTargets.Event | AttributeTargets.Field)]
public sealed class ModApiAttribute : Attribute
{
}
