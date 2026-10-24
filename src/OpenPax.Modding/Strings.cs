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
    public const string AssemblyAndScript = "mod.toml names both an assembly and a script; a mod has one or the other.";
    public const string ScriptMissing = "Mod \"{0}\": its script {1} is not in its folder.";
    public const string ScriptFailed = "Mod \"{0}\": its script failed: {1}";
    public const string HookFailed = "Mod \"{0}\" hit an error and skipped that step: {1}";
    public const string SaveModMissing = "This game was played with the mod {0}, which is not turned on. The game goes on without it, and keeps its saved state for when it is turned on again.";
    public const string SaveModChanged = "The mod {0} was version {1} when this game was saved; it is now {2}.";
    public const string SaveModAdded = "The mod {0} was not in this game when it was saved; it joins it now.";
}
