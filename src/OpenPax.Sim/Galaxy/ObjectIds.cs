namespace OpenPax.Sim.Galaxy;

/// <summary>
/// Hands out object ids in creation order, as the original's global counter
/// does. The original only resets it when a game is torn
/// down, so a second game in the same session starts past the first one's
/// ids; each port game owns its own counter starting at 0.
/// </summary>
public sealed class ObjectIds
{
    public int Next { get; private set; }

    /// <summary>Sets the counter, as a loaded original save does.</summary>
    internal void Restore(int next) => Next = next;

    public int Take()
    {
        return Next++;
    }
}
