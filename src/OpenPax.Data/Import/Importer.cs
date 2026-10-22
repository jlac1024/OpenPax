using OpenPax.Data.Install;
using OpenPax.Data.Png;
using OpenPax.Data.Resources;
using OpenPax.Data.Tables;

namespace OpenPax.Data.Import;

/// <summary>
/// Turns a verified GOG install into the port's data folder: art and fonts
/// converted to PNG, movies to MP4, tables, sounds and music copied as they
/// are (the port reads those formats directly). Refuses anything that is
/// not the GOG build.
/// </summary>
public static class Importer
{
    public const string ImportVersion = "5";
    public const string StampFile = "import.toml";

    private static readonly string[] _copiedFolders = ["Data", "Sound", "MUSIC"];

    // How much of the whole each part of an import is, roughly, by time.
    private const double PicturesShare = 0.25;
    private const double UpscaleShare = 0.35;
    private const double MoviesShare = 0.3;

    /// <param name="ffmpeg">The ffmpeg tool (tools/build_ffmpeg.sh, or the one a release ships) to convert the movies; with none, the game is imported without its movies, which it then does not play.</param>
    /// <param name="progress">Told how far the import has got as it goes.</param>
    /// <param name="modernFonts">A folder of the modern text font (Titillium Web) to copy in, as a release ships it beside the program; null for none.</param>
    public static bool Run(string source, string outFolder, string? ffmpeg, TextWriter log, IProgress<ImportProgress>? progress = null, string? modernFonts = null)
    {
        void Report(double fraction, string step) => progress?.Report(new ImportProgress(Math.Clamp(fraction, 0, 1), step));

        var game = GameInstall.Resolve(source);
        if (game is null)
        {
            log.WriteLine(string.Format(Strings.ImportNoGame, source));
            return false;
        }

        Report(0, Strings.ImportChecking);
        var check = GameInstall.Verify(game);
        log.WriteLine(check.Describe());
        if (!check.IsGogBuild)
        {
            return false;
        }

        // Into a folder beside the real one, which takes its place only when
        // the import is whole: a stopped or failed import leaves no half data.
        var work = outFolder.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) + ".importing";
        if (Directory.Exists(work))
        {
            Directory.Delete(work, recursive: true);
        }

        Directory.CreateDirectory(work);
        var palettes = ArtImporter.ImportPalettes(game, work);
        log.WriteLine(string.Format(Strings.ImportPalettes, palettes.Count));
        var images = Directory.GetFiles(Path.Combine(game, "Image"), "*.img").Order(StringComparer.Ordinal).ToArray();
        for (var i = 0; i < images.Length; i++)
        {
            Report(PicturesShare * i / images.Length, Strings.ImportStepPictures);
            var result = ArtImporter.ImportImageFile(images[i], work, palettes);
            log.WriteLine(string.Format(Strings.ImportPictures, Path.GetFileName(images[i]), result.Pictures, result.UncertainPalettes));
        }

        log.WriteLine(string.Format(Strings.ImportFonts, FontImporter.ImportAll(game, work)));
        if (modernFonts is not null && Directory.Exists(modernFonts))
        {
            CopyTree(modernFonts, Path.Combine(work, "fonts", "modern"));
        }

        log.WriteLine(string.Format(Strings.ImportUpscaled, UpscaleImporter.UpscaleAll(work, (done, all) => Report(PicturesShare + (UpscaleShare * done / Math.Max(1, all)), Strings.ImportStepUpscale))));

        var exe = File.ReadAllBytes(Path.Combine(game, GameInstall.Executable));
        if (ExecutableIcon.Read(exe) is { } icon)
        {
            using var iconFile = File.Create(Path.Combine(work, ExecutableIcon.FileName));
            PngWriter.Write(iconFile, icon);
            log.WriteLine(Strings.ImportIcon);
        }

        var tables = Directory.CreateDirectory(Path.Combine(work, "tables")).FullName;
        File.WriteAllText(Path.Combine(tables, DirectionTables.FileName), DirectionTables.FromExecutable(exe).ToToml());
        log.WriteLine(string.Format(Strings.ImportTables, DirectionTables.FileName));
        var combat = ResourceFile.Load(Path.Combine(game, "Image", "Combat.img"));
        File.WriteAllText(Path.Combine(tables, SpriteRows.FileName), SpriteRows.ToToml(SpriteRows.Measure(combat)));
        log.WriteLine(string.Format(Strings.ImportTables, SpriteRows.FileName));

        var moviesAt = PicturesShare + UpscaleShare;
        if (ffmpeg is null || !File.Exists(ffmpeg))
        {
            log.WriteLine(Strings.ImportNoFfmpeg);
        }
        else if (!MovieImporter.ImportAll(game, work, ffmpeg, log, (done, all) => Report(moviesAt + (MoviesShare * done / Math.Max(1, all)), Strings.ImportStepMovies)))
        {
            return false;
        }

        Report(moviesAt + MoviesShare, Strings.ImportStepCopy);
        foreach (var folder in _copiedFolders)
        {
            CopyTree(Path.Combine(game, folder), Path.Combine(work, "original", folder));
        }

        File.WriteAllText(Path.Combine(work, StampFile), $"import_version = \"{ImportVersion}\"\ngog_build = \"2.0.0.12\"\n");
        if (Directory.Exists(outFolder))
        {
            Directory.Delete(outFolder, recursive: true);
        }

        Directory.Move(work, outFolder);
        Report(1, Strings.ImportStepDone);
        log.WriteLine(string.Format(Strings.ImportDone, outFolder));
        return true;
    }

    /// <summary>Whether a folder holds a whole import of this version, so the game can run from it.</summary>
    public static bool IsImported(string folder)
    {
        var stamp = Path.Combine(folder, StampFile);
        return File.Exists(stamp) && File.ReadAllText(stamp).Contains($"import_version = \"{ImportVersion}\"", StringComparison.Ordinal);
    }

    private static void CopyTree(string from, string to)
    {
        foreach (var path in Directory.EnumerateFiles(from, "*", SearchOption.AllDirectories))
        {
            var target = Path.Combine(to, Path.GetRelativePath(from, path));
            // target is a file path under 'to', so it always has a parent directory.
            Directory.CreateDirectory(Path.GetDirectoryName(target)!);
            File.Copy(path, target);
        }
    }
}
