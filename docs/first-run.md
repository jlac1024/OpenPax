# First run

OpenPax ships with none of the game. The first time it starts with no
imported data, it shows a setup screen that finds the player's own copy of
Pax Imperia: Eminent Domain from GOG and imports it into the user folder.

## Where it looks

- An import already beside the program (a `data/` folder next to it, as
  the development build has) is used as it is.
- Otherwise data goes in the user folder (`SDL_GetPrefPath`, `OpenPax/OpenPax`),
  under `data/`.
- Installed copies: GOG's folders on Windows (`GOG Games`, GOG Galaxy's
  `Games`), and on Linux and macOS the places GOG's installer, innoextract,
  Wine, Lutris and CrossOver leave it.
- GOG's offline installer (`setup_pax_imperia_*.exe`) in Downloads or on the
  Desktop.

The player can also pick a folder or an installer themselves.

## GOG's two installers

GOG offers two downloads for the game. Only the **offline backup installer**,
`setup_pax_imperia_2.0.0.12.exe` (about 545 MB), holds the game, and the
setup reads it directly ([formats/inno-setup.md](formats/inno-setup.md)):
no need to run it, and no Windows needed. The other, the **Galaxy
installer** `GOG_Galaxy_Pax_Imperia_-_Eminent_Domain.exe` (under 1 MB), only
downloads the game through GOG Galaxy. The setup tells the two apart by
their contents, not their names, and when the Galaxy installer is picked
(or is the only thing found) it says that it holds no game and to download
the offline backup installer from the GOG library.

## The import

From an installer, the game's files are first unpacked beside the data
folder (`data.unpacking/`, removed afterward) and checked against the
installer's own SHA-1s. Then, from that or an installed folder:

1. The files are checked against the GOG build's (sizes and SHA-256).
2. Pictures, palettes, fonts and tables are converted; pictures are also
   remade at 2x.
3. The movies are converted to MP4 with the `ffmpeg` tool shipped in
   `tools/` beside the program. Without it the game is imported with no
   movies.

All of it goes into `data.importing/`, which takes the place of `data/` only
once the import is whole, so a stopped or failed import leaves nothing
half done. The setup shows progress and, on failure, why.
