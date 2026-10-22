namespace OpenPax.Sim;

/// <summary>
/// The sort of the original's C runtime (MSVC 5 qsort). It is
/// not stable, and the game sorts records with equal keys (the wormhole
/// network sorts star pairs by integer distance), so the order of equal
/// elements decides game outcomes. This reproduces it element for element.
/// </summary>
public static class MsvcSort
{
    // Ranges this long or shorter go to the selection sort.
    private const int ShortRange = 8;

    public static void Sort<T>(Span<T> items, Comparison<T> compare)
    {
        if (items.Length < 2)
        {
            return;
        }

        var pending = new Stack<(int Lo, int Hi)>();
        var lo = 0;
        var hi = items.Length - 1;
        while (true)
        {
            var size = hi - lo + 1;
            if (size <= ShortRange)
            {
                SelectionSort(items, lo, hi, compare);
            }
            else
            {
                // The middle element becomes the pivot at lo.
                Swap(items, lo + size / 2, lo);
                var up = lo;
                var down = hi + 1;
                while (true)
                {
                    do
                    {
                        up++;
                    }
                    while (up <= hi && compare(items[up], items[lo]) <= 0);

                    do
                    {
                        down--;
                    }
                    while (down > lo && compare(items[down], items[lo]) >= 0);

                    if (down < up)
                    {
                        break;
                    }

                    Swap(items, up, down);
                }

                Swap(items, lo, down);

                // Continue with the smaller side and keep the larger for
                // later. The original compares the sides in bytes with an
                // extra -1, which in elements means "left <= right".
                if (down - lo <= hi - up)
                {
                    if (up < hi)
                    {
                        pending.Push((up, hi));
                    }

                    if (lo + 1 < down)
                    {
                        hi = down - 1;
                        continue;
                    }
                }
                else
                {
                    if (lo + 1 < down)
                    {
                        pending.Push((lo, down - 1));
                    }

                    if (up < hi)
                    {
                        lo = up;
                        continue;
                    }
                }
            }

            if (pending.Count == 0)
            {
                return;
            }

            (lo, hi) = pending.Pop();
        }
    }

    // move the first largest element to the end, shrink, repeat.
    private static void SelectionSort<T>(Span<T> items, int lo, int hi, Comparison<T> compare)
    {
        for (; hi > lo; hi--)
        {
            var max = lo;
            for (var p = lo + 1; p <= hi; p++)
            {
                if (compare(items[p], items[max]) > 0)
                {
                    max = p;
                }
            }

            Swap(items, max, hi);
        }
    }

    private static void Swap<T>(Span<T> items, int a, int b)
    {
        if (a != b)
        {
            (items[a], items[b]) = (items[b], items[a]);
        }
    }
}
