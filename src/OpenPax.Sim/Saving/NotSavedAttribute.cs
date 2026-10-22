namespace OpenPax.Sim.Saving;

/// <summary>A field the save format leaves out, and so network states and state merges too (a game's recording).</summary>
[AttributeUsage(AttributeTargets.Field)]
public sealed class NotSavedAttribute : Attribute
{
}
