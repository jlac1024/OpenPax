# LZMA and LZMA2

What GOG's installer is packed with ([inno-setup.md](inno-setup.md)).
`src/OpenPax.Data/Compression/` decodes both, written from the format's
published description, checked against streams that liblzma packed and
against every file in the installer.

## LZMA

A range coder turns the packed bytes back into bits. Each bit is weighed
by an 11-bit probability that adapts after every bit (by a 32nd of the
distance to certainty). The decoder starts on five bytes, the first always
zero.

The output is a run of packets, each chosen by modeled bits that depend on
a 12-value state (what the last few packets were) and the low bits of the
output position:

- **literal:** a byte coded down a 256-leaf tree, the tree picked by the
  position's low `lp` bits and the previous byte's top `lc` bits. Right
  after a match, the byte at the last distance guides the first bits until
  they differ.
- **match:** a length (2 to 273, coded in three bands of 8, 8 and 256
  values) and a new distance: a 6-bit slot picked by the length, then
  extra bits, the middle ones modeled for short distances and plain for
  long ones, and the lowest four modeled.
- **short repeat:** one byte from the last distance.
- **repeat match:** a length and one of the last four distances.

A distance of all ones is the end mark. The settings `lc`, `lp` and `pb`
come packed in one byte as `(pb * 5 + lp) * 9 + lc`.

## LZMA2

A byte giving the dictionary size (`(2 | b & 1) << (b / 2 + 11)`), then
chunks, ended by a zero byte:

- `01`: stored, and the dictionary is cleared; `02`: stored. Then the size
  less one (16-bit, big endian) and the bytes.
- `80` to `FF`: LZMA. The low five bits and the next 16 (big endian) are
  the unpacked size less one; then the packed size less one (16-bit). Bits 5
  and 6 say what is reset: 0 nothing, 1 the models, 2 the models with new
  settings (one more byte, as above), 3 that and the dictionary. Every LZMA
  chunk starts a fresh range coder; its unpacked size ends it.
