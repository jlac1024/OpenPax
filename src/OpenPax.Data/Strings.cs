namespace OpenPax.Data;

/// <summary>User-facing strings for the data layer.</summary>
internal static class Strings
{
    public const string BadTypeName = "Resource type must be four ASCII characters: '{0}'";
    public const string NotAContainer = "{0}: not a resource file";
    public const string Truncated = "{0}: file ends inside the directory";
    public const string BadOffset = "{0}: resource {1} {2} points outside the file";
    public const string UnsortedIds = "{0}: resource ids of type {1} are not in increasing order";
    public const string DuplicateType = "{0}: type {1} appears twice";
    public const string NoSuchType = "{0}: no resources of type {1}";
    public const string NoSuchResource = "{0}: no resource {1} {2}";
    public const string BadPalette = "Not a palette resource";
    public const string BadPaletteSize = "A palette has 256 colors, not {0}";
    public const string BadPicture = "Not a valid picture resource";
    public const string BadPictureSize = "Pixel count does not match the picture size";
    public const string BadPreferences = "Not a valid preferences file";
    public const string BadExecutable = "The game executable does not hold the expected data";
    public const string ImportTables = "Tables: {0}";
    public const string ImportIcon = "Icon: the executable's";
    public const string TomlBadLine = "Cannot read TOML line {0}";
    public const string TomlMissing = "TOML value '{0}' is missing or has the wrong type";
    public const string BadView = "Not a valid window layout";
    public const string UnknownViewClass = "Window layout uses unknown element class {0}";
    public const string BadTech = "Not a valid technology record";
    public const string BadRace = "Not a valid species record";
    public const string BadHull = "Not a valid hull resource";
    public const string BadFont = "Not a valid bitmap font file";
    public const string BadStringList = "Not a valid string list resource";
    public const string BadPng = "Not a PNG file this reader understands: {0}";
    public const string ImportUnpacking = "Unpacking GOG's installer";
    public const string ImportUnpackingFrom = "Unpacking {0} files from {1}";
    public const string ImportUnpackFailed = "Could not unpack the installer: {0}";
    public const string ImportNoGame = "No Pax Imperia: Eminent Domain install found in {0}";
    public const string ImportPalettes = "Palettes: {0}";
    public const string ImportPictures = "{0}: {1} pictures, {2} with an uncertain palette";
    public const string ImportFonts = "Fonts: {0}";
    public const string ImportUpscaled = "Pictures remade at 2x: {0}";
    public const string ImportMovies = "Movies: {0}";
    public const string ImportMovieFailed = "Could not convert {0}: {1}";
    public const string ImportNoFfmpeg = "No ffmpeg tool to convert the movies (--ffmpeg PATH): imported without them";
    public const string ImportChecking = "Checking the game's files";
    public const string ImportStepPictures = "Converting the pictures";
    public const string ImportStepUpscale = "Drawing the pictures at twice the size";
    public const string ImportStepMovies = "Converting the movies";
    public const string ImportStepCopy = "Copying the sounds, music and game tables";
    public const string ImportStepDone = "Done";
    public const string FfmpegDidNotStart = "ffmpeg did not start";
    public const string ImportDone = "Imported into {0}";
    public const string VerifyOk = "Found the GOG build of Pax Imperia: Eminent Domain in {0}";
    public const string VerifyFailed = "{0} is not the GOG build of Pax Imperia: Eminent Domain (2.0.0.12). Only that build is supported.";
    public const string VerifyMissing = "  missing: {0}";
    public const string VerifyDifferent = "  different: {0}";
    public const string VerifyMore = "  and {0} more";

    // Content packs (docs/modding/content.md).
    public const string ContentBadValue = "{0} must be a {1}.";
    public const string ContentNoKey = "a new entry needs a key, a change needs the id of what it changes";
    public const string ContentNoTech = "there is no tech {0}";
    public const string ContentKeyTaken = "the key \"{0}\" is taken";
    public const string ContentNoClass = "a new tech needs a class or a like";
    public const string ContentBadClass = "class must be star_drive, tactical_drive, sensor, ecm, weapon, shield, fighter, armor, special or improvement";
    public const string ContentNoField = "{0}: there is no field \"{1}\"; left as it was";
    public const string ContentNoHull = "type must be a hull type, 0 to 5";
    public const string ContentBadLike = "like must be an atmosphere class, 0 to 4";
    public const string ContentBadBreathable = "breathable must be like, all, none or anaerobic";
    public const string ContentNoName = "a design needs a name";
    public const string LzmaCorrupt = "The packed data is damaged (LZMA).";
    public const string LzmaTruncated = "The packed data ends too soon (LZMA).";
    public const string InnoNotInstaller = "That is not GOG's offline installer for the game.";
    public const string InnoGalaxyStub = "That is GOG's small Galaxy installer, which holds none of the game: download the offline backup installer from your GOG library.";
    public const string InnoVersion = "The installer is in a form this cannot read ({0}); only GOG's installer setup_pax_imperia_2.0.0.12.exe is known.";
    public const string InnoCompression = "The installer packs its files in a way this cannot read (method {0}).";
    public const string InnoEncrypted = "The installer's files are locked with a password.";
    public const string InnoDamaged = "The installer is damaged: download it again from your GOG library.";
    public const string InnoBadFile = "The installer is damaged ({0} does not match its checksum): download it again from your GOG library.";
}
