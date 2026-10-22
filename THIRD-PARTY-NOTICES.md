# Third-party notices

OpenPax is licensed under the GNU General Public License, version 3 (see
`LICENSE`). Builds of OpenPax include or use the following, each under its
own license. Their sources are pinned in `tools/deps.toml` and are built
from source by `build.sh`.

| Component | Used for | License |
|---|---|---|
| [SDL 3](https://libsdl.org) | window, input, sound output, timing | zlib |
| [FFmpeg](https://ffmpeg.org) libavutil, libavcodec, libavformat | playing the movies; a decode-only build (H.264, AAC, MP4) | LGPL 2.1 or later |
| FFmpeg program, with [x264](https://www.videolan.org/developers/x264.html) | converting the game's movies on import, on the player's machine | GPL 2 or later |
| [Titillium Web](https://fonts.google.com/specimen/Titillium+Web) | the modern text font | SIL Open Font License 1.1 (`OFL.txt` ships beside the font) |
| [.NET](https://dotnet.microsoft.com) runtime | the program itself, published self-contained | MIT |

Used only to develop OpenPax, never shipped:

| Component | Used for | License |
|---|---|---|
| [xUnit.net](https://xunit.net) | tests | Apache 2.0 |
| [unshield](https://github.com/twogood/unshield) | comparing the original release's update package | MIT |

## Source of FFmpeg and x264

The release bundles include FFmpeg's libraries (LGPL) and an FFmpeg
program built with x264 (GPL). Each release on GitHub carries, beside the
bundles, the exact source archives they were built from:

- `source-ffmpeg-VERSION.tar.xz`, FFmpeg as released at
  <https://ffmpeg.org/releases/>
- `source-x264-COMMIT.tar.bz2`, x264 from
  <https://code.videolan.org/videolan/x264> at that commit

The versions, download addresses and SHA-256 hashes are pinned in
`tools/deps.toml`. How they are
configured and built is in this repository: `tools/build_ffmpeg.sh`
(Linux), `tools/build_native_windows.sh` (Windows) and
`tools/build_native_macos.sh` (macOS). The archives on each release are the
ones its bundles were built from.

No part of *Pax Imperia: Eminent Domain* is included. Its art, sound,
music, movies, text and data are read at run time from the player's own
copy of the game.
