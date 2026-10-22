namespace OpenPax.Media;

/// <summary>User-facing strings for the media decoders.</summary>
internal static class Strings
{
    public const string NotWav = "Not a WAVE file";
    public const string NotPcm = "Only uncompressed PCM WAVE files are supported";
    public const string BadWav = "Damaged or unsupported WAVE file";
    public const string BadOgg = "Damaged Ogg file";
    public const string UnsupportedPixelFormat = "Movie uses an unsupported pixel format ({0})";
    public const string UnsupportedSampleFormat = "Movie uses an unsupported sample format ({0})";
    public const string NotVorbis = "Not an Ogg Vorbis stream";
    public const string BadVorbisSetup = "Damaged or unsupported Vorbis setup";
    public const string Floor0 = "Vorbis floor type 0 is not supported (no current encoder produces it)";
}
