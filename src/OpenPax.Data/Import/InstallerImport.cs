using OpenPax.Data.Install;
using OpenPax.Data.Installer;

namespace OpenPax.Data.Import;

/// <summary>
/// The import from GOG's offline installer (docs/first-run.md): the game's
/// files are unpacked beside the data folder, imported like an installed
/// copy, and the unpacked files removed again.
/// </summary>
public static class InstallerImport
{
    // Unpacking is quick next to the pictures and movies.
    private const double UnpackShare = 0.15;

    /// <summary>Takes the same arguments as <see cref="Importer.Run"/>, with the installer's path as the source.</summary>
    public static bool Run(string installer, string outFolder, string? ffmpeg, TextWriter log, IProgress<ImportProgress>? progress = null, string? modernFonts = null)
    {
        var unpack = outFolder.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) + ".unpacking";
        try
        {
            if (Directory.Exists(unpack))
            {
                Directory.Delete(unpack, recursive: true);
            }

            progress?.Report(new ImportProgress(0, Strings.ImportUnpacking));
            var gog = GogInstaller.Open(installer);
            log.WriteLine(string.Format(Strings.ImportUnpackingFrom, gog.Files.Count(), installer));
            var game = Path.Combine(unpack, GameInstall.FolderName);
            gog.Extract(game, new UnpackProgress(progress, UnpackShare));
            return Importer.Run(game, outFolder, ffmpeg, log, new ScaledProgress(progress, UnpackShare, 1 - UnpackShare), modernFonts);
        }
        catch (InvalidDataException e)
        {
            log.WriteLine(e.Message);
            return false;
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            log.WriteLine(string.Format(Strings.ImportUnpackFailed, e.Message));
            return false;
        }
        finally
        {
            try
            {
                if (Directory.Exists(unpack))
                {
                    Directory.Delete(unpack, recursive: true);
                }
            }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException)
            {
                // Left for the next import to clear: it deletes the folder first.
            }
        }
    }
}
