namespace OpenPax.Audio;

/// <summary>
/// The music (thread, next track, battle
/// ). A request names a track: the title (2) and setup (3)
/// tracks loop alone; any of 4 to 6 plays the battle tracks and any of 7 to
/// 14 the game's tracks, one after another, each set starting after the
/// track it played last. A request within the set already playing changes
/// nothing. During a battle every request is ignored while music plays.
/// </summary>
public sealed class MusicPlayer(IMusicSource source)
{
    private const int FirstBattleTrack = 4;
    private const int LastBattleTrack = 6;
    private const int FirstGameTrack = 7;
    private const int LastGameTrack = 14;
    private const int GameMusic = 14;

    private int _request;
    private int[] _list = [];
    private bool _battleSet;
    private int _battleTrack;
    private int _gameTrack;
    private bool _battle;
    private IMusicStream? _stream;

    /// <summary>The track playing, 0 for none.</summary>
    public int Track { get; private set; }

    public bool Paused { get; set; }

    private bool Running => _stream is not null;

    private static bool BattleSet(int t) => t is >= FirstBattleTrack and <= LastBattleTrack;

    private static bool GameSet(int t) => t is >= FirstGameTrack and <= LastGameTrack;

    public void Request(int track)
    {
        if (_battle)
        {
            if (Running)
            {
                return;
            }

            track = FirstBattleTrack;
            _request = 0;
        }

        if (track == _request || (BattleSet(track) && BattleSet(_request)) || (GameSet(track) && GameSet(_request)))
        {
            return;
        }

        _request = track;
        if (BattleSet(track))
        {
            (_list, _battleSet) = ([4, 5, 6], true);
        }
        else if (GameSet(track))
        {
            (_list, _battleSet) = ([7, 8, 9, 10, 11, 12, 13, 14], false);
        }
        else
        {
            (_list, _battleSet) = ([track], false);
        }

        Next();
    }

    /// <summary>
    /// A battle with the local side starts: the stored battle track goes up
    /// one (4 to 6) and is asked for, so the track after it plays: 5, then 4,
    /// 6, 4, 6. It ends: back to the game's tracks. Quirk kept: one flag for
    /// all battles, so the first to end brings the game's music back.
    /// </summary>
    public void Battle(bool on)
    {
        if (on)
        {
            _battleTrack = Math.Clamp(_battleTrack + 1, FirstBattleTrack, LastBattleTrack);
            Request(_battleTrack);
            _battle = true;
        }
        else
        {
            _battle = false;
            Request(GameMusic);
        }
    }

    /// <summary>The next track of the list: a lone track again; else the one after the last played.</summary>
    private void Next()
    {
        int track;
        if (_list.Length == 1)
        {
            track = _list[0];
        }
        else
        {
            ref var stored = ref _battleSet ? ref _battleTrack : ref _gameTrack;
            stored = stored + 1 > _list[^1] || stored < _list[0] ? _list[0] : stored + 1;
            track = stored;
        }

        _stream = source.Open(track);
        Track = _stream is null ? 0 : track;
    }

    /// <summary>Fills the buffer with music, going on to the next track as one ends; silence while paused or with none.</summary>
    public void Read(Span<short> stereo)
    {
        stereo.Clear();
        var done = 0;
        var empty = 0;
        while (!Paused && _stream is not null && done < stereo.Length)
        {
            var frames = _stream.Read(stereo[done..]);
            done += frames * 2;

            // A track that gives nothing twice running is broken: the music stops, as the thread ends on an error.
            empty = frames == 0 ? empty + 1 : 0;
            if (empty > 1)
            {
                _stream = null;
                Track = 0;
            }
            else if (done < stereo.Length)
            {
                Next();
            }
        }
    }
}
