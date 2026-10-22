namespace OpenPax.Modding;

/// <summary>What the mod loader says, in one place.</summary>
internal static class Strings
{
    public const string NoId = "mod.toml has no id.";
    public const string BadId = "The mod id \"{0}\" may hold only letters, digits, '-', '_' and '.'.";
    public const string Unreadable = "Mod in {0} could not be read: {1}";
    public const string Duplicate = "Mod in {0} has the id \"{1}\", already taken by another; left out.";
    public const string NotFound = "Mod \"{0}\" is turned on but not in the mods folder.";
    public const string AssemblyFailed = "Mod \"{0}\": its assembly {1} could not be loaded: {2}";
    public const string NoEntry = "Mod \"{0}\": its assembly has no public class implementing IMod.";
    public const string LoadFailed = "Mod \"{0}\" failed while loading: {1}";
}
