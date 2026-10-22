namespace OpenPax.Data.Installer;

/// <summary>
/// A packed file in the installer: the chunk it is in (by offset from the
/// start of the file data), where it sits in the unpacked chunk, its size,
/// its SHA-1, and how it was packed.
/// </summary>
internal sealed record DataEntry(
    long ChunkOffset,
    long OffsetInChunk,
    long Size,
    long ChunkSize,
    byte[] Sha1,
    bool CallsFiltered,
    bool Compressed,
    bool Encrypted);
