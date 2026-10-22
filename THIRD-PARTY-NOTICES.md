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

No part of *Pax Imperia: Eminent Domain* is included. Its art, sound,
music, movies, text and data are read at run time from the player's own
copy of the game.
