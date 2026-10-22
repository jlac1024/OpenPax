# GOG's offline installer (Inno Setup 5.5.0, Unicode)

`setup_pax_imperia_2.0.0.12.exe` (545 MB) is an Inno Setup installer. The
first run reads it directly (`src/OpenPax.Data/Installer/`), so a player
who has only downloaded it does not have to install the game first. Only
this version of the format is read; anything else is refused with a plain
message. Byte order is little endian.

## Telling it from the Galaxy stub

GOG also offers `GOG_Galaxy_Pax_Imperia_-_Eminent_Domain.exe` (under 1 MB).
It holds none of the game: it downloads the game through GOG Galaxy. Both
are Windows programs named after the game, so the name is not enough.
`InstallerCheck` looks at the first 8 MB only:

- **Offline installer:** the loader table (below) is there, and the offset it
  gives for the setup data points at the text `Inno Setup Setup Data (`.
- **Galaxy stub:** its version information (UTF-16, in the resources, which
  UPX leaves unpacked) names it `GOG Galaxy - Game Installer`.
- Anything else, or a file that does not start with `MZ`, is unknown.

The setup screen warns as soon as a stub is picked, refuses to import it,
and says so up front if the only thing found in Downloads or Desktop is a
stub.

## Loader table

Inside the setup program, found by the 12 bytes
`72 44 6C 50 74 53 CD E6 D7 7B 0B 2A` ("rDlPtS" and six more), then
32-bit values: table version (1), total size, offset and size and CRC of
the packed setup program, **offset of the setup data**, **offset of the
file data**, and the table's own CRC.

## Setup data

A 64-byte version text, padded with zeros:
`Inno Setup Setup Data (5.5.0) (u)`. Then two blocks.

Each block: a CRC-32 (the zlib one) of the next 5 bytes, the stored size
(32-bit), and a byte saying whether it is compressed. The stored bytes are
pieces of up to 4096 bytes, each after its own CRC-32. Joined, they are a
raw LZMA stream: 5 property bytes (packed lc/lp/pb, then the dictionary
size), then the data with no length ahead of it. It is decoded only as far
as it is read.

Strings are a 32-bit byte length, then UTF-16 text. Option sets are a bit
per option in whole bytes; a 3-byte set takes 4. "Windows versions" is 20
bytes (the lowest and the highest version a part is for).

### First block

1. **Header:** 31 strings (names, URLs, folders, license and info texts,
   the compiled script), then 16 counts: languages, messages, permissions,
   types, components, tasks, folders, files, packed files, icons, INI,
   registry, delete, uninstall delete, run, uninstall run. Then Windows
   versions, three colors, the password hash (20) and salt (8), extra disk
   space (64-bit), slices per disk, five one-byte settings, the
   **compression method** (0 stored, 1 zlib, 2 bzip2, 3 LZMA, 4 LZMA2), two
   architecture sets, two page settings, the uninstall size (64-bit), and
   46 options, of which bit 37 means the files are encrypted.
2. **Languages:** 10 strings, language id, four font sizes, right to left.
3. **Messages:** 2 strings and a language index. **Permissions:** 1 string.
4. **Types:** 4 strings, Windows versions, 1 option byte, type, size.
5. **Components:** 5 strings, extra space, level, used, Windows versions,
   1 option byte, size.
6. **Tasks:** 6 strings, level, used, Windows versions, 1 option byte.
7. **Folders:** name and 6 condition strings, attributes, Windows versions,
   permission (16-bit), 1 option byte.
8. **Files**, the part the import needs: source, **destination**, font
   name, assembly name, then the conditions: components, tasks, languages,
   **check**, after install, before install. Windows versions, **location**
   (index into the packed file table), attributes, external size,
   permission, 4 option bytes, and the file type (0 for an ordinary file).

The rest of the block (icons, registry and so on, then the wizard images)
is not read.

### Second block: the packed file table

One 74-byte entry per packed file: first and last slice (both 0 in a
single-file installer), **chunk offset** (from the file data offset),
**offset inside the unpacked chunk** (64-bit), **size** (64-bit), chunk size
(64-bit), **SHA-1**, file time, file version, then 10 options in 2 bytes.
Bit 4 means the call filter below was applied, bit 6 the chunk is
encrypted, bit 7 the chunk is compressed.

## File data

Each chunk starts with `zlb` and 0x1A, then the packed data in the
header's method. GOG's installer uses LZMA2 ([lzma.md](lzma.md)) with one
file per chunk. The file is the bytes from its offset inside the chunk,
for its size, and must match its SHA-1.

**Call filter.** Programs and libraries were changed before packing: after
each `E8` or `E9` byte (x86 CALL and JMP), unless the five bytes would cross
a 64 KB boundary in the file, the next four bytes are an address. If its top
byte is 00 or FF, the low 24 bits were made relative to the place after
the address (its offset in the file, kept to 24 bits), and when bit 23 of
the result is set the top byte was inverted. Undoing that is the reverse
subtraction; either way the reader moves on four bytes.

## Which files go into the game folder

Destinations start with folder constants. The import takes those under
`{app}\` and leaves out `{app}\__support\` (GOG's spare copies for its
repair tool) and `{tmp}\` (the installer's own art). Some places have
several entries, chosen by the installer's script at install time
(`checkIfOverwrite`, `IsWindows10` and the like). The import takes the last
entry with no check, else the last entry, since later entries overwrite
earlier ones. Unpacked that way, every game file matches GOG's installed
copy. Only GOG Galaxy's own files (`goggame-*`, `webcache.zip`) can differ
from a copy installed through Galaxy, which brings its own builds of them.
