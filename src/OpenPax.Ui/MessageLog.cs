namespace OpenPax.Ui;

/// <summary>
/// The message log (app +0x74): notices wait in a queue and
/// one becomes current every 3000 ms on a free-running timer. A new lasting
/// notice blinks its lamp 9 times, 250 ms apart. The current notice and the
/// older lasting ones form a history of 150, newest first; a transient
/// notice is never kept and gives way at once to the next.
/// </summary>
public sealed class MessageLog
{
    public const int HistorySize = 150;
    private const int AdvancePeriod = 3000;
    private const int BlinkPeriod = 250;
    private const int Blinks = 9;

    private readonly Queue<UiNotice> _queue = [];
    private readonly UiNotice?[] _history = new UiNotice?[HistorySize];
    private long _lastAdvance = -1;
    private long _lastBlink;

    /// <summary>Slot 0 is the current notice; the rest are older lasting ones, newest first.</summary>
    public IReadOnlyList<UiNotice?> History => _history;

    public UiNotice? Current => _history[0];

    public int Queued => _queue.Count;

    /// <summary>The notices waiting their turn, oldest first.</summary>
    public IEnumerable<UiNotice> Waiting => _queue;

    /// <summary>The lamp is flashing for a new lasting notice.</summary>
    public bool Blinking { get; private set; }

    /// <summary>Blink steps so far; the lamp shows on odd steps while blinking.</summary>
    public int BlinkCount { get; private set; }

    /// <summary>
    /// Queues a notice. It shows at once if it or the current
    /// one is transient; otherwise it waits for the timer.
    /// </summary>
    public void Add(UiNotice notice, long now)
    {
        _queue.Enqueue(notice);
        if (notice.Transient || Current is { Transient: true })
        {
            Advance(now);
        }
    }

    /// <summary>
    /// The next queued notice becomes current: a transient or
    /// empty current one is dropped, a lasting one moves into the history.
    /// </summary>
    private bool Advance(long now)
    {
        if (!_queue.TryDequeue(out var next))
        {
            return false;
        }

        if (Current is { Transient: false })
        {
            Array.Copy(_history, 0, _history, 1, HistorySize - 1);
        }

        _history[0] = next;
        if (!next.Transient)
        {
            Blinking = true;
            _lastBlink = now;
        }

        return true;
    }

    /// <summary>The idler: blink steps, and the advance every 3000 ms (a blink step skips only the date redraw).</summary>
    public void Tick(long now)
    {
        if (_lastAdvance < 0)
        {
            _lastAdvance = now;
        }

        if (Blinking && now - _lastBlink >= BlinkPeriod)
        {
            _lastBlink = now;
            BlinkCount++;
            if (BlinkCount >= Blinks)
            {
                (BlinkCount, Blinking) = (0, false);
            }
        }

        if (now - _lastAdvance >= AdvancePeriod)
        {
            _lastAdvance = now;
            if (!Advance(now))
            {
                Blinking = false;
            }
        }
    }

    /// <summary>Forgets the history, as a new game does. Quirk kept: the queue stays.</summary>
    public void ClearHistory()
    {
        Array.Clear(_history);
        (BlinkCount, Blinking) = (0, false);
    }
}
