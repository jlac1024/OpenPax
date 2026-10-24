namespace OpenPax.Lua.Runtime;

/// <summary>
/// A Lua table: keys 1 to n in an array, any other key in a hash part that
/// keeps the order keys were first added. pairs and next go through the
/// array, then the rest in that order, the same on every machine (Lua
/// itself leaves the order open, and a mod's results must not differ).
/// </summary>
public sealed class LuaTable
{
    private readonly List<LuaValue> _array = [];
    private readonly Dictionary<LuaValue, int> _index = [];
    private readonly List<(LuaValue Key, LuaValue Value)> _entries = [];
    private int _removed;

    public LuaTable? Metatable { get; set; }

    /// <summary>The border # gives: the array part's length, trailing nils left out.</summary>
    public long Length
    {
        get
        {
            var n = _array.Count;
            while (n > 0 && _array[n - 1].IsNil)
            {
                n--;
            }

            if (n == _array.Count && n == 0 && _index.Count > 0)
            {
                // Keys added out of order (t[2] before t[1]) live in the hash part: count them on from 1.
                long k = 0;
                while (Get(LuaValue.Of(k + 1)) is { IsNil: false })
                {
                    k++;
                }

                return k;
            }

            return n;
        }
    }

    public LuaValue Get(LuaValue key)
    {
        var k = key.AsKey();
        if (k.IsInteger && k.AsInteger >= 1 && k.AsInteger <= _array.Count)
        {
            return _array[(int)(k.AsInteger - 1)];
        }

        return _index.TryGetValue(k, out var at) ? _entries[at].Value : LuaValue.Nil;
    }

    public LuaValue Get(string key) => Get(LuaValue.Of(key));

    public LuaValue Get(long key) => Get(LuaValue.Of(key));

    /// <summary>Sets a key; nil removes it. A nil or NaN key is an error, as in Lua.</summary>
    public void Set(LuaValue key, LuaValue value)
    {
        var k = key.AsKey();
        if (k.IsNil)
        {
            throw new LuaException("table index is nil");
        }

        if (k.IsFloat && double.IsNaN(k.AsFloat))
        {
            throw new LuaException("table index is NaN");
        }

        if (k.IsInteger && k.AsInteger >= 1 && k.AsInteger <= _array.Count + 1 && !_index.ContainsKey(k))
        {
            var i = (int)(k.AsInteger - 1);
            if (i < _array.Count)
            {
                _array[i] = value;
                return;
            }

            if (value.IsNil)
            {
                return;
            }

            _array.Add(value);
            MoveFollowingIntoArray();
            return;
        }

        if (_index.TryGetValue(k, out var at))
        {
            // A removed key keeps its place until the next compaction, so next() can go on past it.
            _entries[at] = (k, value);
            if (value.IsNil)
            {
                _removed++;
            }

            return;
        }

        if (value.IsNil)
        {
            return;
        }

        if (_removed > 16 && _removed > _entries.Count / 2)
        {
            Compact();
        }

        _index[k] = _entries.Count;
        _entries.Add((k, value));
    }

    public void Set(string key, LuaValue value) => Set(LuaValue.Of(key), value);

    public void Set(long key, LuaValue value) => Set(LuaValue.Of(key), value);

    /// <summary>The key after this one and its value (nil starts; a nil key back means done), as next() gives them.</summary>
    public (LuaValue Key, LuaValue Value) Next(LuaValue key)
    {
        var at = 0;
        var k = key.AsKey();
        if (!k.IsNil)
        {
            if (k.IsInteger && k.AsInteger >= 1 && k.AsInteger <= _array.Count)
            {
                at = (int)k.AsInteger;
            }
            else if (_index.TryGetValue(k, out var entry))
            {
                at = _array.Count + entry + 1;
            }
            else
            {
                throw new LuaException("invalid key to 'next'");
            }
        }

        for (; at < _array.Count; at++)
        {
            if (!_array[at].IsNil)
            {
                return (LuaValue.Of(at + 1L), _array[at]);
            }
        }

        for (var e = at - _array.Count; e < _entries.Count; e++)
        {
            if (!_entries[e].Value.IsNil)
            {
                return _entries[e];
            }
        }

        return (LuaValue.Nil, LuaValue.Nil);
    }

    /// <summary>The array part's values 1 to n, for table.insert, remove and sort.</summary>
    internal List<LuaValue> Array => _array;

    // t[n+1] just went into the array: keys n+2 and on that were in the hash part follow it.
    private void MoveFollowingIntoArray()
    {
        while (_index.Count > 0 && _index.TryGetValue(LuaValue.Of(_array.Count + 1L), out var at))
        {
            var value = _entries[at].Value;
            _index.Remove(_entries[at].Key);
            _entries[at] = (LuaValue.Nil, LuaValue.Nil);
            _removed++;
            if (value.IsNil)
            {
                break;
            }

            _array.Add(value);
        }
    }

    private void Compact()
    {
        var kept = _entries.Where(e => !e.Value.IsNil).ToList();
        _entries.Clear();
        _index.Clear();
        foreach (var e in kept)
        {
            _index[e.Key] = _entries.Count;
            _entries.Add(e);
        }

        _removed = 0;
    }
}
