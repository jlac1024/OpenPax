namespace OpenPax.Ui;

/// <summary>
/// How a screen grows in a window larger than 640x480 (docs/ui/widescreen.md):
/// spans of columns and of rows of its art and layout that take the extra
/// width and height, each set in equal shares, and bands of rows that repeat
/// at a period across instead of stretching (a ruler, whose ticks must keep
/// their spacing). A screen with no row spans keeps its 480 rows. The
/// extra width comes in whole steps (a table's column, so its tiled columns
/// close cleanly); what is left over is bars. With RepeatRows the row spans
/// repeat their rows down instead of stretching them: a span one period of
/// a grid keeps the grid's lines evenly spaced. Smooth columns (frame
/// metal) do not repeat: each takes its most common color in the span,
/// straight down, so the metal keeps its bevel across and shows no steps.
/// PaneRows gives a panel (by id), and all inside it, row spans of its own
/// in place of the screen's: a panel whose art a screen row span would cut
/// through (a button's outline repeated) grows where its art allows.
/// </summary>
public sealed record WideLayout(
    IReadOnlyList<(int Column, int Span)> Spans,
    IReadOnlyList<(int Top, int Bottom, int Period)> Tiled,
    IReadOnlyList<(int Row, int Span)>? RowSpans = null,
    int WidthStep = 2,
    bool RepeatRows = false,
    IReadOnlyList<(int Left, int Right)>? SmoothColumns = null,
    IReadOnlyDictionary<int, IReadOnlyList<(int Row, int Span)>>? PaneRows = null)
{
    public WideLayout(int column, int span)
        : this([(column, span)], [])
    {
    }

    public IReadOnlyList<(int Row, int Span)> Rows => RowSpans ?? [];

    /// <summary>The extra width each column span takes: equal shares, the remainder to the last.</summary>
    public int[] Shares(int extra) => Split(extra, Spans.Count);

    /// <summary>The extra height each row span takes.</summary>
    public int[] RowShares(int extra) => Split(extra, Rows.Count);

    /// <summary>Where a pane of the original layout goes across: moved by the spans before it, grown by those it crosses.</summary>
    public (int Shift, int Grow) For(int left, int width, int extra) => Place(Spans, Shares(extra), left, width);

    /// <summary>The same down, by the row spans.</summary>
    public (int Shift, int Grow) ForRows(int top, int height, int extra) => Place(Rows, RowShares(extra), top, height);

    /// <summary>The source and screen extents of each piece across (or down) the picture: as it is, or stretched.</summary>
    internal static List<(int From, int Length, int To, int ToLength)> Pieces(IReadOnlyList<(int Start, int Span)> spans, int[] shares, int size)
    {
        var pieces = new List<(int, int, int, int)>();
        var (from, shift) = (0, 0);
        for (var i = 0; i < spans.Count; i++)
        {
            var (start, span) = spans[i];
            pieces.Add((from, start - from, from + shift, start - from));
            pieces.Add((start, span, start + shift, span + shares[i]));
            (from, shift) = (start + span, shift + shares[i]);
        }

        pieces.Add((from, size - from, from + shift, size - from));
        return pieces;
    }

    private static (int Shift, int Grow) Place(IReadOnlyList<(int Start, int Span)> spans, int[] shares, int start, int length)
    {
        var (shift, grow) = (0, 0);
        for (var i = 0; i < spans.Count; i++)
        {
            var (s, span) = spans[i];
            if (s + span <= start)
            {
                shift += shares[i];
            }
            else if (s < start + length)
            {
                grow += shares[i];
            }
        }

        return (shift, grow);
    }

    private static int[] Split(int extra, int count)
    {
        var shares = new int[count];
        for (var i = 0; i < count; i++)
        {
            shares[i] = extra / count;
        }

        if (count > 0)
        {
            shares[^1] += extra - (extra / count * count);
        }

        return shares;
    }
}
