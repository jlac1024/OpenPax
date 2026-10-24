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
folder holds `.openpax-packaged`, naming the package.

- **Flatpak:** UPDATE asks the Flatpak portal to install the newest
  version from the repository the Flatpak came from (`FlatpakPortal`),
  without leaving the sandbox, and the game restarts into it with
  `flatpak-spawn --latest-version`. The first time, the system asks the
  player whether OpenPax may update itself. The game speaks D-Bus with a
  small client of its own (`Update/DBus`): the portal keeps an update
  monitor only while the connection that made it is open, so the monitor
  is made, Update asked for and its Progress signals followed on one
  connection. If it fails, the popup says why and tells the player to
  update with their package manager (the software center, or `flatpak
  update`).
- **Installed from outside while running** (`InstallWatch`): a Flatpak
  keeps an update monitor open with the portal, whose UpdateAvailable
  signal says when the installed version is no longer the running one
  (the portal looks every five minutes); a .deb looks at its program file
  every 15 seconds, which apt upgrade replaces. The main menu then offers
  RESTART or LATER, once a run; over a game it waits for the menu.
  Tested on lin-dev: the running Flatpak noticed a `flatpak update` from
  outside and offered the restart.
- **.deb:** updating needs an administrator, so the popup says the
  system's updates bring the new version (Software Updater, or `sudo apt
  update && sudo apt upgrade`), from the apt repository the package set up.

Tested on lin-dev (2026-10-24) with Flatpak unpacked in /projects/store:
version A, installed from a signed local repository, updated itself to
version B through the portal. The restart could not be shown there, as
the portal starts `/usr/bin/flatpak`, which that machine does not have.

On macOS the install folder is the one holding `OpenPax.app`. A bundle the
game downloads itself carries no download quarantine, so `prepare_mac.sh`
is needed only for the first install.

## Versions

Release bundles are built with `-p:Version=VERSION` (`tools/package.sh`);
the game reads its informational version. The assembly version stays
`1.0.0.0` in every build, since mods bind to `OpenPax.Modding` by it.

## Trying it

- `OPENPAX_UPDATE_NOW=1` installs the update a check finds without
  asking (with `OPENPAX_PRETEND_VERSION`, an update to try without a click).
- `OPENPAX_PRETEND_VERSION=0.1.0` makes a build check, and offer updates, as
  that version (development builds included).
- `OPENPAX_NETWORK_TESTS=1 dotnet test --project tests/OpenPax.Host.Tests`
  runs the test that updates an empty folder from GitHub for real (about
  90 MB).
