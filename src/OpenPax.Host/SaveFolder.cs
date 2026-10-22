using OpenPax.Sim;
using OpenPax.Sim.Saving;
using OpenPax.Sim.Saving.Original;
using OpenPax.Ui;

namespace OpenPax.Host;

/// <summary>
/// The saved games: files in "Saved Games" in the user folder, one per name
/// with the extension .hgsave. A name is used as typed, except characters a
/// file name cannot hold, which become "_". Saves of the original game
/// copied into the folder (files with no extension, as it names them) are
/// listed and loaded too. Only files that are saves are listed, by name.
/// </summary>
internal sealed class SaveFolder(string folder, GameLauncher launcher, TextWriter errors) : ISaveGames
{
    public const string FolderName = "Saved Games";
    public const string Extension = ".hgsave";

    private static readonly char[] _forbidden = [.. Path.GetInvalidFileNameChars(), '/', '\\', ':', '*', '?', '"', '<', '>', '|'];

    public IReadOnlyList<string> Names()
    {
        if (!Directory.Exists(folder))
        {
            return [];
        }

        var names = new List<string>();
        foreach (var path in Directory.EnumerateFiles(folder))
        {
            try
            {
                using var file = File.OpenRead(path);
                if (path.EndsWith(Extension, StringComparison.Ordinal) ? SaveFile.IsSave(file) : OriginalSave.IsSave(file))
                {
                    names.Add(Path.GetFileNameWithoutExtension(path));
                }
            }
            catch (IOException)
            {
                // A file that cannot be opened is not listed.
            }
        }

        names = [.. names.Distinct()];

        names.Sort(StringComparer.Ordinal);
        return names;
    }

    public bool Save(string name, SavedSession session)
    {
        try
        {
            Directory.CreateDirectory(folder);

            // Written beside the old one first, so a failed save leaves it whole.
            var path = PathOf(name);
            var temporary = path + ".new";
            using (var file = File.Create(temporary))
            {
                SaveFile.Write(file, session, launcher.Tables);
            }

            File.Move(temporary, path, overwrite: true);
            return true;
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or NotSupportedException)
        {
            errors.WriteLine(string.Format(Strings.SaveFailed, name, e.Message));
            return false;
        }
    }

    public SavedSession? Load(string name)
    {
        try
        {
            if (!File.Exists(PathOf(name)) && File.Exists(OriginalPathOf(name)))
            {
                using var original = File.OpenRead(OriginalPathOf(name));
                return FromOriginal(original);
            }

            using var file = File.OpenRead(PathOf(name));
            var saved = SaveFile.Read(file, launcher.Tables, new OpenPax.Ai.ComputerPlayer(), typeof(SavedSession).Assembly) as SavedSession;

            // The mods' step rules act on a loaded game too (docs/modding.md).
            if (saved is not null)
            {
                launcher.Mods.Attach(saved.Game);
            }

            return saved;
        }
        catch (Exception e) when (e is IOException or InvalidDataException or UnauthorizedAccessException or EndOfStreamException or ArgumentOutOfRangeException or InvalidCastException)
        {
            errors.WriteLine(string.Format(Strings.LoadFailed, name, e.Message));
            return null;
        }
    }

    public void Delete(string name)
    {
        try
        {
            File.Delete(File.Exists(PathOf(name)) ? PathOf(name) : OriginalPathOf(name));
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            errors.WriteLine(string.Format(Strings.SaveFailed, name, e.Message));
        }
    }

    /// <summary>An original save: the game, the star in view and the selection, found again by id.</summary>
    private SavedSession FromOriginal(Stream file)
    {
        Game game;
        int currentStar;
        int selectionId;
        try
        {
            (game, currentStar, selectionId) = OriginalSave.Load(file, launcher.Tables, new OpenPax.Ai.ComputerPlayer());
        }
        catch (Exception e) when (e is not (OutOfMemoryException or InvalidDataException))
        {
            // A damaged file can fail anywhere in the reading; it is simply not a save we can read.
            throw new InvalidDataException(e.Message, e);
        }

        launcher.Mods.Attach(game);
        object? selection = null;
        foreach (var star in game.Universe.Stars)
        {
            selection ??= star.ObjectId == selectionId ? star : null;
            selection ??= star.Planets.FirstOrDefault(p => p.ObjectId == selectionId);
            selection ??= star.Fleets.FirstOrDefault(f => f.ObjectId == selectionId);
            selection ??= star.Wormholes.FirstOrDefault(w => w.ObjectId == selectionId);
        }

        return new SavedSession
        {
            Game = game,
            CurrentStar = Math.Clamp(currentStar, 0, game.Universe.Stars.Count - 1),
            Selection = selection,
            LastDesignSlot = -1,
            DomesticFilters = [true, true, true, true, true],
            DomesticSortColumn = 1,
            DesignCategory = 26011,
        };
    }

    /// <summary>An original save's file, by the name it is listed under; nothing outside the folder.</summary>
    private string OriginalPathOf(string name) => Path.Combine(folder, Path.GetFileName(name));

    private string PathOf(string name)
    {
        var safe = new string([.. name.Select(c => _forbidden.Contains(c) ? '_' : c)]);
        return Path.Combine(folder, safe + Extension);
    }
}
