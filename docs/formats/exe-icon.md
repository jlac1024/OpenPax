# The executable's icon

The window icon comes from `Pax Imperia.exe` itself, read at import into
`data/icon.png` (`ExecutableIcon`).

The executable is a Windows PE file. Its resource directory (data
directory 2 of the optional header) is a tree: type, then name, then
language, each level a table of entries holding an id and an offset. A
set top bit on the offset points to the next table; a clear one points to
a data entry, which holds the data's address (relative to the image) and
size. Addresses map to file offsets through the section table.

An icon group (type 14) lists its images after a 6 byte header, 14 bytes
each: width, height, color count, a reserved byte, planes, bits per
pixel, size and the id of the icon (type 3) that holds the image. Each
icon is a bitmap header followed by the palette (4 bytes a color, blue
first), the pixels bottom row first with rows padded to 4 bytes, and a
1 bit mask of the same shape where a set bit means see-through.

The GOG build has one group with one image: 32x32 in 16 colors, the
yellow emblem on black. Its mask is empty, so the black square shows, as
it did on Windows. It becomes an indexed PNG with one extra palette entry for
the see-through pixels. The host scales it up 4 times, whole pixels at a
time, and gives it to SDL as the window icon.
