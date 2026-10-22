# Sound and music

The original plays sound through the Miles Sound System (22050 Hz 16-bit
stereo, whole files in memory) and music as CD audio, which GOG's
`winmm.dll` serves from `MUSIC/TrackNN.ogg`. The port mixes both itself at
44100 Hz stereo (the music's own rate) and hands the mix to SDL3. With no
sound device the game runs silent. Code: `src/OpenPax.Audio` (`Mixer`,
`SoundBank`, `SoundIds`, `MusicPlayer`, `OggMusicStream`, `AudioOutput`),
`src/OpenPax.Host/SoundFiles.cs`, and the calls in `src/OpenPax.Ui`.
Study note: build/scratch-ui/sound.md.

## Sounds

Each sound has an id; the table of which file each id plays is fixed in
the code, file names matched without regard to case. Ids
below 33000 are effects: they may play over themselves, at the effects
volume. The rest are voices and the two ambience loops: one already
playing is not started again, at the voice volume. A sound keeps the
volume it started at. Everything plays centered. At most 16 sounds play at
once; a new one past that is dropped.

What plays:
- Widgets play their layout's sound id on press and the id plus 100 on a
  release over them (buttons, check boxes, hot spots, state boxes, empire
  toggles); spinners play the id on a step up and plus 100 on a step down,
  nothing at a limit. The top bar has its own (history toggle, back arrow).
- The bridge hums (45100) and the species screen has its ambience (45000)
  while they are open. The species lineup plays the figures' steps.
- Voices: species accepted, game saved, the battle window opening, battle
  stations when a battle with the player's side starts, the answers to map
  and battle orders (picked at random outside the game's random numbers),
  and the notices for research done, a ship finished, a colony lost, a
  treaty offered, espionage results and trade answers.
- The Game Options volume sliders play a sample at the new level.
- In the battle window: each new shot its weapon's sound, each explosion
  when first drawn (so a slow frame can miss one, as in the original).

## Music

Each screen asks for its music as it opens: the title track (2), the
setup screens' track (3), or the game's tracks (7 to 14 in turn); the
battle window asks for none. A request within the set already playing
changes nothing, so the game's tracks run on across screens. A battle with
the player's side plays the battle tracks (4 to 6; the first battle starts
on 5, then 4, 6, 4, 6, as the original's track counter runs), and every
request is ignored until it ends; then the game's tracks go on after the
last one played. Music pauses while a movie plays.

## Movies

A movie's own sound plays through the mixer at the effects volume, as the
original's Smacker player used it, and stops at once when the movie is
skipped.

## Volumes

Effects, voices and music each from their preference (0 to 127, the Game
Options sliders store 12 per level), with the original's master volume of
100 of 127 on effects and voices. The music volume applies from the start
(decisions.md).

## Not done

The code-made sliders' sounds (classes 8012 and 8031), and
the network screens, which are not ported.
