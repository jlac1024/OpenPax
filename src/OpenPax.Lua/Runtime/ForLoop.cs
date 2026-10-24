namespace OpenPax.Lua.Runtime;

/// <summary>
/// The two for loops as Lua 5.4 runs them. A numeric loop over integers
/// counts its turns first, so it cannot overflow; each turn has a fresh
/// variable, so closures made in the body keep their own.
/// </summary>
internal static class ForLoop
{
    public static Signal Numeric(Frame f, LuaValue start, LuaValue limit, LuaValue step, int slot, Stat body)
    {
        var state = f.State;
        if (!start.IsNumber)
        {
            throw state.Error("'for' initial value must be a number");
        }

        if (!limit.IsNumber)
        {
            throw state.Error("'for' limit must be a number");
        }

        if (!step.IsNumber)
        {
            throw state.Error("'for' step must be a number");
        }

        if (start.IsInteger && step.IsInteger)
        {
            var (i, by) = (start.AsInteger, step.AsInteger);
            if (by == 0)
            {
                throw state.Error("'for' step is zero");
            }

            if (IntegerLimit(limit, by) is not { } last || (by > 0 ? i > last : i < last))
            {
                return Signal.Normal;
            }

            // The number of turns after the first, unsigned so the widest range still fits.
            var turns = by > 0 ? (ulong)(last - i) / (ulong)by : (ulong)(i - last) / (by == long.MinValue ? (ulong)long.MaxValue + 1 : (ulong)-by);
            while (true)
            {
                f.Locals[slot] = new Box(LuaValue.Of(i));
                state.Spend();
                var signal = body(f);
                if (signal == Signal.Break)
                {
                    return Signal.Normal;
                }

                if (signal == Signal.Return)
                {
                    return signal;
                }

                if (turns-- == 0)
                {
                    return Signal.Normal;
                }

                i = unchecked(i + by);
            }
        }

        var (x, end, d) = (start.AsFloat, limit.AsFloat, step.AsFloat);
        if (d == 0)
        {
            throw state.Error("'for' step is zero");
        }

        for (; d > 0 ? x <= end : x >= end; x += d)
        {
            f.Locals[slot] = new Box(LuaValue.Of(x));
            state.Spend();
            var signal = body(f);
            if (signal == Signal.Break)
            {
                return Signal.Normal;
            }

            if (signal == Signal.Return)
            {
                return signal;
            }
        }

        return Signal.Normal;
    }

    // A float limit is taken down (or up, counting down) to a whole number; null when the loop cannot run.
    private static long? IntegerLimit(LuaValue limit, long step)
    {
        if (limit.IsInteger)
        {
            return limit.AsInteger;
        }

        var d = limit.AsFloat;
        if (double.IsNaN(d))
        {
            return null;
        }

        var whole = step > 0 ? Math.Floor(d) : Math.Ceiling(d);
        return whole >= 9.2233720368547758E18 ? long.MaxValue : whole <= -9.2233720368547758E18 ? long.MinValue : (long)whole;
    }

    public static Signal Generic(Frame f, LuaValue[] iterator, int[] slots, Stat body)
    {
        var state = f.State;
        var (function, invariant, control) = (iterator[0], iterator[1], iterator[2]);
        while (true)
        {
            var values = state.Call(function, [invariant, control]);
            var first = values.Length > 0 ? values[0] : LuaValue.Nil;
            if (first.IsNil)
            {
                return Signal.Normal;
            }

            control = first;
            for (var k = 0; k < slots.Length; k++)
            {
                f.Locals[slots[k]] = new Box(k < values.Length ? values[k] : LuaValue.Nil);
            }

            var signal = body(f);
            if (signal == Signal.Break)
            {
                return Signal.Normal;
            }

            if (signal == Signal.Return)
            {
                return signal;
            }
        }
    }
}
