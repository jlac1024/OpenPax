namespace OpenPax.Net.Messages;

/// <summary>A piece of a mod package (docs/modding/multiplayer.md): from the host to the lobby, and from the lobby to those who asked. Pieces come in order.</summary>
/// <param name="Total">The whole package's length in bytes.</param>
/// <param name="Offset">Where this piece starts in it.</param>
public sealed record ModChunkMessage(string Key, long Total, long Offset, byte[] Data) : NetMessage
{
    /// <summary>The most a piece holds, well inside a message.</summary>
    public const int MaxData = 256 * 1024;
}
