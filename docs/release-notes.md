OpenPax is an open-source engine for Pax Imperia: Eminent Domain. It comes
with none of the game: you need your own copy from GOG.

## Getting started

1. Download the bundle for your system below and unpack it.
   - **Windows (x64):** `OpenPax-…-win-x64-setup.exe` installs it (no admin needed, with a Start menu entry); or unzip `OpenPax-…-win-x64.zip` and run `openpax.exe`.
   - **Linux (x64):** `OpenPax-…-linux-x64.tar.gz`, then run `./openpax`.
   - **macOS (Apple Silicon):** `OpenPax-…-osx-arm64.tar.gz`. The app is
     not signed by a known developer: run `prepare_mac.sh` once from the
     unpacked folder, then open `OpenPax.app`.
2. On first start, OpenPax looks for your copy of the game. Point it at
   either:
   - an installed copy (the folder holding `Pax Imperia.exe`), or
   - GOG's **offline backup installer**, `setup_pax_imperia_2.0.0.12.exe`,
     from your GOG library. It is read directly; there is no need to run it.

   The small **GOG Galaxy installer** holds none of the game and will not
   work: OpenPax tells you if you pick it.
3. The import converts the game's art, sound and movies once, into your
   user folder. Then the game starts.

The sample mods (Simple Combat, Bounty, Arsenal) are included and off
until turned on in Options. The modding guide is in `docs/modding/`.
