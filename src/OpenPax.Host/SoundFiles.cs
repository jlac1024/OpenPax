using OpenPax.Audio;

namespace OpenPax.Host;

/// <summary>
/// The game's sound and music files in the imported data. The original names
/// them in mixed case and the files are mostly upper case, so they are found
/// without regard to case.
/// </summary>
internal static class SoundFiles
{
    /// <summary>The mixer with the game's sounds and the music tracks.</summary>
    public static Mixer Load(OpenPax.Data.DataFiles files)
    {
        // Through the data packs (docs/modding.md): a pack's sound or track stands in for the game's.
        var sounds = Index(files.FilesUnder("original", "Sound"));
        var tracks = Index(files.FilesUnder("original", "MUSIC"));
        byte[]? Read(Dictionary<string, string> index, string name) =>
            index.TryGetValue(name.ToLowerInvariant(), out var path) ? File.ReadAllBytes(path) : null;
        var bank = new SoundBank(path => Read(sounds, path));
        var music = new MusicPlayer(new OggMusicSource(track => Read(tracks, $"track{track:00}.ogg")));
        return new Mixer(bank, music);
    }

    /// <summary>The files by their lower-case relative paths, with '/' separators.</summary>
    private static Dictionary<string, string> Index(IReadOnlyDictionary<string, string> files) =>
        files.GroupBy(f => f.Key.Replace('\\', '/').ToLowerInvariant()).ToDictionary(g => g.Key, g => g.First().Value);
}
