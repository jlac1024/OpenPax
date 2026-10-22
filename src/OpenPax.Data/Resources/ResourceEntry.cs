namespace OpenPax.Data.Resources;

/// <summary>
/// One directory entry. <see cref="Length"/> is not stored in the file;
/// the original never needs it because every resource body reads itself
/// from the stream. We derive it from the next resource's offset.
/// </summary>
public readonly record struct ResourceEntry(int Id, int Offset, int Length);
