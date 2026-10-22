namespace OpenPax.Modding;

/// <summary>
/// A C# mod's entry point (docs/modding.md): a public class with a public
/// constructor taking nothing. The game makes one when it starts and calls
/// <see cref="Load"/> once, where the mod says what it changes.
/// </summary>
public interface IMod
{
    void Load(ModContext context);
}
