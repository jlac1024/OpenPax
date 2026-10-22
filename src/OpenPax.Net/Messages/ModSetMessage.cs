namespace OpenPax.Net.Messages;

/// <summary>A lobby member's mods as they are now, after loading another set in place (docs/modding/multiplayer.md): mod keys in order.</summary>
public sealed record ModSetMessage(string Mods) : NetMessage;
