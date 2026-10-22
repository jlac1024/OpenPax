using System.Globalization;
using System.Text;
using OpenPax.Data.Toml;

namespace OpenPax.Host.Headless;

/// <summary>
/// A replay: the new game settings, the human players (none in a game of
/// computer players only), the commands applied, each with how many steps
/// had run before it, and the state hash after every step. Stored as TOML;
/// a command is its save-format bytes in base64.
/// </summary>
internal sealed record Replay(uint Seed, int Stars, int Players, int Difficulty, IReadOnlyList<ulong> Hashes)
{
    public IReadOnlyList<ReplayHuman> Humans { get; init; } = [];

    public IReadOnlyList<(long Step, byte[] Command)> Commands { get; init; } = [];

    public void Save(string path)
    {
        var text = new StringBuilder("# OpenPax replay: new game settings, humans, commands and the state hash after each step.\n");
        text.Append(CultureInfo.InvariantCulture, $"seed = {Seed}\nstars = {Stars}\nplayers = {Players}\ndifficulty = {Difficulty}\n");
        foreach (var h in Humans)
        {
            text.Append(CultureInfo.InvariantCulture, $"\n[[human]]\nname = \"{Escape(h.Name)}\"\nflag = {h.Flag}\ncolor = {h.Color}\nspecies = {h.Species}\n");
        }

        text.Append("\n# Lists span lines; our TOML reader reads them line by line.\nhashes = [\n");
        foreach (var h in Hashes)
        {
            // Strings because TOML integers are signed 64 bit.
            text.Append(CultureInfo.InvariantCulture, $"  \"{h:x16}\",\n");
        }

        text.Append("]\ncommands = [\n");
        foreach (var (step, command) in Commands)
        {
            text.Append(CultureInfo.InvariantCulture, $"  \"{step}:{Convert.ToBase64String(command)}\",\n");
        }

        text.Append("]\n");
        File.WriteAllText(path, text.ToString());
    }

    public static Replay Load(string path)
    {
        var lines = File.ReadAllLines(path);
        var header = TomlReader.Parse(string.Join('\n', lines.TakeWhile(l => !l.StartsWith("hashes", StringComparison.Ordinal))));
        var hashes = List(lines, "hashes")
            .Select(l => ulong.Parse(l, NumberStyles.HexNumber, CultureInfo.InvariantCulture))
            .ToList();
        var commands = List(lines, "commands")
            .Select(l => (long.Parse(l[..l.IndexOf(':')], CultureInfo.InvariantCulture), Convert.FromBase64String(l[(l.IndexOf(':') + 1)..])))
            .ToList();
        var humans = TomlReader.Tables(header, "human")
            // "colour": replays written before the project moved to American spelling.
            .Select(h => new ReplayHuman(TomlReader.String(h, "name"), TomlReader.Int(h, "flag"), TomlReader.Int(h, h.ContainsKey("color") ? "color" : "colour"), TomlReader.Int(h, "species")))
            .ToList();
        return new Replay(
            checked((uint)(long)header["seed"]),
            TomlReader.Int(header, "stars"),
            TomlReader.Int(header, "players"),
            TomlReader.Int(header, "difficulty"),
            hashes)
        {
            Humans = humans,
            Commands = commands,
        };
    }

    /// <summary>The quoted entries of a list that spans lines, from "key = [" to "]".</summary>
    private static IEnumerable<string> List(string[] lines, string key) => lines
        .SkipWhile(l => !l.StartsWith(key + " = [", StringComparison.Ordinal))
        .Skip(1)
        .TakeWhile(l => l.Trim() != "]")
        .Select(l => l.Trim().TrimEnd(',').Trim('"'))
        .Where(l => l.Length > 0);

    private static string Escape(string text) => text.Replace("\\", "\\\\", StringComparison.Ordinal).Replace("\"", "\\\"", StringComparison.Ordinal);
}
