# Updates

OpenPax keeps itself up to date from its releases on GitHub
(`src/OpenPax.Host/Update/`). Nothing of the player's is touched: saves,
settings and the imported game live in the user folder, not the install.

## Checking

On start, when **Check for updates on start** is on (Options, UPDATES), the
game asks GitHub's API for the repository's releases, in the background. It
takes the newest one that:

- is published (not a draft);
- is not a pre-release, unless **Include pre-releases** is on (on by
  default while OpenPax is 0.x, as every release so far is a pre-release);
- is newer than the running version (semantic version order: `0.2.0-beta.1`
  comes before `0.2.0`);
- has a bundle for this platform, named as `tools/package.sh` names it
  (`OpenPax-VERSION-win-x64.zip`, `-linux-x64.tar.gz`, `-osx-arm64.tar.gz`),
  downloaded from this repository's releases, with the SHA-256 GitHub
  records for every release file.

No answer, or no GitHub, is quiet on start; **CHECK NOW** in Options says
what it found or why it could not. A development build (version
`0.0.0-dev`, anything not packaged as a release) never checks on start.

## Installing

When an update is found, the main menu offers it once a run, in the game's
yes or no popup. YES (or **UPDATE** in Options):

1. downloads the bundle into `.openpax-update/` in the install folder,
   checking its size and SHA-256 as it comes; CANCEL stops it;
2. unpacks it beside the download;
3. swaps it in, entry by entry: what was there moves into `.openpax-old/`
   first. A running program cannot be overwritten on Windows but can be
   renamed, so this works with the game running. If any step fails,
   everything moved goes back and the popup says why;
4. starts the new version with the same arguments, and closes.

The next start deletes `.openpax-old/` and `.openpax-update/`.

Kept through an update:

- `config.json` beside the program, which is edited by hand (the lobby's
  address);
- mods the player added to the `mods/` folder beside the shipped ones;
- anything else in the install folder that the bundle does not have (a
  `data/` folder beside the program, for one).

An install in a folder the player cannot change (Program Files without
admin rights, say) is not updated in place: the popup says so and points
to the release page.

A .deb or Flatpak install (platforms.md) is the package manager's: its
folder holds `.openpax-packaged`, naming the package, and the popup says
the system's updater brings the new version (from the package
repositories the package set up) instead of downloading the bundle.

On macOS the install folder is the one holding `OpenPax.app`. A bundle the
game downloads itself carries no download quarantine, so `prepare_mac.sh`
is needed only for the first install.

## Versions

Release bundles are built with `-p:Version=VERSION` (`tools/package.sh`);
the game reads its informational version. The assembly version stays
`1.0.0.0` in every build, since mods bind to `OpenPax.Modding` by it.

## Trying it

- `OPENPAX_PRETEND_VERSION=0.1.0` makes a build check, and offer updates, as
  that version (development builds included).
- `OPENPAX_NETWORK_TESTS=1 dotnet test --project tests/OpenPax.Host.Tests`
  runs the test that updates an empty folder from GitHub for real (about
  90 MB).
