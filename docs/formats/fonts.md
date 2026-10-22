# Bitmap fonts

`Data/pft8`, `pft9`, `pft10`, `pft11`, `pft12`, `pft14`, `pft18`: one file
per point size, loaded at startup through the font
constructor and loader. Little endian.

## Layout

| Size | Meaning |
|---|---|
| 2 | `II`, the stream's byte order mark |
| | Regular glyph set (two metrics) |
| | Bold glyph set (one metric) |

Each glyph set:

| Size | Meaning |
|---|---|
| 2 | First character code (int16). Negative means the set is absent. |
| 2 | Last character code (int16) |
| 4 x n | Metrics: two int32 for the regular set, one for the bold set. Stored even when the set is absent. For `pft8` regular they are 11 and 7 (likely line height and baseline). Exact use is settled when text drawing is ported. |
| | For each code from first to last: int32 width in pixels; if not 0, int32 byte count, then the bitmap |

Bitmaps are 1 bit per pixel, most significant bit first, rows padded to
whole bytes, so the byte count is rows x ((width + 7) / 8).

The original keeps glyph records at slot code + 2 (regular) and
code + 0x101 (bold). `pft18` has only the bold set, `pft8` only the
regular one, the rest both.

## Character set

Characters are laid out in Windows-1252: 0x80 to 0x9F hold the Windows
extras (curly quotes, ellipsis, dagger, per mille, trademark and so on).
All game strings are plain ASCII, but the string reader decodes
Windows-1252 so other text matches the fonts.

## Converted form

The importer writes `fonts/pft<size>-regular.png` and
`fonts/pft<size>-bold.png`: 16 glyphs per row, each in a cell one pixel
wider than the widest glyph. Index 0 is transparent background and
index 1 white ink; text color is applied when drawing. `fonts/pft<size>.toml`
holds each set's first and last code, metrics, and per glyph its code,
position in the sheet, width and row count.
