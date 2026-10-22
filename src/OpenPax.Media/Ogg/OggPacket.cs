namespace OpenPax.Media.Ogg;

/// <summary>
/// One logical packet. <see cref="GranulePosition"/> is the position of the
/// page the packet ends on, or -1 when another packet ends later on that
/// page (only the last packet ending on a page carries it).
/// </summary>
public sealed record OggPacket(byte[] Data, long GranulePosition, bool IsLastOfStream);
