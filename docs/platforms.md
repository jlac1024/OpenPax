# Windows and macOS

Phase 8 builds the game for Windows x64 and Apple Silicon Macs besides
Linux (decisions.md, 2026-10-22). Everything is built on the Linux machine
except the Mac's native libraries, which are built once on the Mac.

## What differs between platforms

| | Linux | Windows x64 | macOS (Apple Silicon) |
|---|---|---|---|
| Program | `openpax` | `openpax.exe` (no console window) | `OpenPax.app` |
| SDL3 | `libSDL3.so.0` | `SDL3.dll` | `libSDL3.0.dylib` |
| Movie libraries | `libavcodec.so.N` and others | `avcodec-N.dll` and others | `libavcodec.N.dylib` and others |
| Built by | build.sh | tools/build_native_windows.sh, on Linux with MinGW | tools/build_native_macos.sh, on the Mac |
| Config and saves | `~/.local/share/OpenPax/OpenPax/` | `%APPDATA%\OpenPax\OpenPax\` | `~/Library/Application Support/OpenPax/OpenPax/` |

The game's data, the sample mods, the docs and config.json are the same
files everywhere. The program itself is .NET, published for each
platform from Linux (`dotnet publish -r win-x64` / `osx-arm64`),
self-contained.

The simulation gives the same results on every platform: it uses no
platform maths library for anything that reaches a result
(docs/numerics.md), so a game saved on one plays on identically on
another. The movie bindings read FFmpeg's structs at offsets generated on
Linux; the Windows and Mac builds check them against their own compilers
(tools/check_ffmpeg_abi.sh). The one error code that differs, EAGAIN, is
picked when the game runs.

## Making the builds

```bash
./build.sh --dist
```

This builds everything as usual, then:

- `build/dist/OpenPax-win-x64.zip`: the game for Windows.
- `build/dist/OpenPax-osx-arm64.tar.gz`: the game for the Mac, once the
  Mac's native libraries are in `build/` (below). Until then,
  `build/dist/macos-native-kit.zip`: what the Mac needs to build them.

## Releases

GitHub Actions makes the release bundles (`.github/workflows/release.yml`)
for a version tag such as `v0.1.0`, or by hand as a draft:

- **Linux x64** on Ubuntu 22.04 (for an older glibc): `build.sh`, then
  `tools/package.sh linux-x64 VERSION`.
- **Windows x64** on Ubuntu, cross-compiled: `tools/build_native_windows.sh`,
  then `tools/package.sh win-x64 VERSION`.
  A Windows runner then makes the installer from that bundle with Inno
  Setup (`tools/installer/openpax.iss`): `OpenPax-VERSION-win-x64-setup.exe`,
  beside the zip. It installs for the user who runs it, with no admin
  prompt, into `%LOCALAPPDATA%\Programs\OpenPax`, with a Start menu entry,
  a desktop shortcut if wanted, and an uninstaller. Per user because the
  game updates itself in place (updates.md), which a Program Files install
  would not let it do. An upgrade keeps the player's `config.json`.
- **Linux packages** on Ubuntu 24.04, from the Linux bundle:
  `tools/package_deb.sh VERSION` makes `openpax_VERSION_amd64.deb` (the
  bundle in `/opt/openpax`, the `openpax` and `openpax-lobby` commands, a
  menu entry and the icon), and `tools/package_flatpak.sh VERSION` makes
  `OpenPax-VERSION-linux-x64.flatpak` (app ID `io.github.jlac1024.OpenPax`,
  on the freedesktop runtime from Flathub). The Flatpak copies the finished
  bundle in rather than building it in `flatpak-builder`, so it needs the
  runtime but not the SDK. It may use the window, GPU, sound and network,
  and read the Downloads folder and `~/GOG Games`; any other game folder or
  installer comes through the file picker (the desktop portal). The menu
  entry and app data are in `tools/linux/`. The job installs each package
  and starts the game once. Both packages leave `.openpax-packaged` in the
  install folder: the package manager owns those files, so the game leaves
  updating to the system (updates.md).
- **Package repositories** on the project's GitHub Pages
  (`https://jlac1024.github.io/OpenPax`), published by
  `.github/workflows/repositories.yml` when a release is published (never a
  draft), from that release's `.flatpak` and `.deb` (`tools/publish_repos.sh`):
  a Flatpak repository (`flatpak/repo`) and an apt repository (`apt`,
  "stable main"), both signed with the key in the `OPENPAX_SIGNING_KEY`
  secret, whose public half is `tools/linux/openpax-signing-key.asc`. With
  that file in place the packages point at them: the `.flatpak` names the
  Flatpak repository and its key (`build-bundle --repo-url --gpg-keys`), and
  the `.deb` installs `/etc/apt/sources.list.d/openpax.list` and the key in
  `/usr/share/keyrings`. Installing either once is enough: the software
  center, `flatpak update` and `apt upgrade` bring later versions. The
  repositories hold only the latest published release.

The icon is OpenPax's own (`src/OpenPax.Host/Icon/openpax.svg`, drawn for
the project). `tools/make_icon.sh` renders it to the Windows `.ico` (the
program's, so shortcuts and the installer use it), the Mac `.icns` and the
raw pixels of the window's icon; the results are committed.
- **macOS, Apple Silicon** on a GitHub Mac: `tools/build_native_macos.sh`,
  then `tools/package.sh osx-arm64 VERSION`, signed ad hoc.

`tools/package.sh` makes `build/release/OpenPax-VERSION-RID` and its archive. The program and the lobby server are each one file holding all their .NET libraries (single-file publishing, debug symbols inside, not trimmed, so mods can use any of .NET); the native libraries (SDL3 and the LGPL FFmpeg ones, which a player must be able to replace) stay beside them. The bundle holds:
the program, the lobby server, the native libraries, the movie converter,
the modern font, the sample mods that need no game data, the modding docs,
`config.json`, the license and notices. No game data. `ci.yml` builds and
tests every push to `main`. The bundles are the same ones `build.sh --dist`
made before, without the machine-specific `deploy/` setup.

The game updates itself from these releases ([updates.md](updates.md)).

## On Windows

1. Copy `OpenPax-win-x64.zip` to the PC and unpack it anywhere.
2. Run `openpax.exe` in the `OpenPax-win-x64` folder.
3. Windows may warn that it is from an unknown publisher (it is not
   signed): More info, then Run anyway.

The lobby server for Windows is in `lobby-server\openpax-lobby.exe`.

## On the Mac

The first time only, build the native libraries:

1. Install the Xcode command line tools: `xcode-select --install`.
2. Install CMake (cmake.org, or `brew install cmake`).
3. Copy `macos-native-kit.zip` to the Mac, unpack it, and in Terminal:

   ```bash
   cd macos-native-kit
   ./tools/build_native_macos.sh
   ```

   It downloads the pinned SDL3 and FFmpeg sources, checks their hashes,
   builds them (several minutes), checks FFmpeg's struct offsets, and
   writes `native-osx-arm64.tar.gz` in the kit folder.
4. Copy `native-osx-arm64.tar.gz` back to the Linux machine's `build/`
   folder, and run `./build.sh --dist` again.

Then, each time:

1. Copy `OpenPax-osx-arm64.tar.gz` to the Mac and unpack it.
2. Once per copy, in Terminal, in the unpacked folder: `./prepare_mac.sh`.
   macOS refuses apps that are not signed by a known developer; this
   clears the download mark and signs the app for this Mac alone.
3. Open `OpenPax.app`.

The lobby server for the Mac is in `lobby-server/openpax-lobby`.

## Phase 8's exit

- **The same save on all three**: save a game on one machine, copy its
  file from `Saved Games` in the config folder (table above) to the same
  place on the others, and load it there.
- **A network game across all three**: run a lobby server (`./run.sh
  --lobby-server` on Linux, or the public lobby), connect all three, play
  a game, and watch for a desync report.
