namespace OpenPax.Net.Lobby;

/// <summary>
/// The lobby's mod packages (docs/modding/multiplayer.md): those uploaded
/// whole, by key, within a byte budget (the longest unused go first), those
/// coming in from a host, and who waits for each.
/// </summary>
internal sealed class ModCache(long budget)
{
    private readonly Dictionary<string, (byte[] Package, long Used)> _done = new(StringComparer.Ordinal);
    private readonly Dictionary<string, (MemoryStream Bytes, long Total, int From)> _coming = new(StringComparer.Ordinal);
    private readonly Dictionary<string, List<LobbyMember>> _waiting = new(StringComparer.Ordinal);
    private long _clock;

    public byte[]? Get(string key)
    {
        if (!_done.TryGetValue(key, out var entry))
        {
            return null;
        }

        _done[key] = (entry.Package, ++_clock);
        return entry.Package;
    }

    /// <summary>Whether a host has been asked for the package and it is on its way.</summary>
    public bool Coming(string key) => _coming.ContainsKey(key);

    public void Wait(string key, LobbyMember member)
    {
        if (!_waiting.TryGetValue(key, out var list))
        {
            _waiting[key] = list = [];
        }

        if (!list.Contains(member))
        {
            list.Add(member);
        }
    }

    /// <summary>A host was asked for a package: its pieces are taken from that member only.</summary>
    public void Expect(string key, int from) => _coming[key] = (new MemoryStream(), -1, from);

    /// <summary>A piece from a host; the whole package and those waiting for it once the last piece is in, else null.</summary>
    public (byte[] Package, List<LobbyMember> Waiting)? Add(string key, int from, long total, long offset, byte[] data, long maxPackage)
    {
        if (!_coming.TryGetValue(key, out var upload) || upload.From != from || total > maxPackage
            || (upload.Total >= 0 && upload.Total != total) || offset != upload.Bytes.Length)
        {
            return null;
        }

        upload.Bytes.Write(data);
        _coming[key] = (upload.Bytes, total, from);
        if (upload.Bytes.Length < total)
        {
            return null;
        }

        _coming.Remove(key);
        var package = upload.Bytes.ToArray();
        Store(key, package);
        var waiting = _waiting.Remove(key, out var list) ? list : [];
        return (package, waiting);
    }

    /// <summary>A host gone: what it was sending is dropped, and those waiting on it learn nothing more.</summary>
    public void Forget(LobbyMember member)
    {
        foreach (var key in _coming.Where(c => c.Value.From == member.Id).Select(c => c.Key).ToList())
        {
            _coming.Remove(key);
        }

        foreach (var list in _waiting.Values)
        {
            list.Remove(member);
        }
    }

    private void Store(string key, byte[] package)
    {
        _done[key] = (package, ++_clock);
        while (_done.Values.Sum(v => (long)v.Package.Length) > budget && _done.Count > 1)
        {
            _done.Remove(_done.MinBy(e => e.Value.Used).Key);
        }
    }
}
