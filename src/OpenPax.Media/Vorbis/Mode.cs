namespace OpenPax.Media.Vorbis;

/// <summary>A Vorbis mode (spec 4.2.4): long or short block and which mapping to use.</summary>
internal sealed record Mode(bool LongBlock, int Mapping);
