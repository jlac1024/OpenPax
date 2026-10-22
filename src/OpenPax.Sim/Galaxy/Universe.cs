namespace OpenPax.Sim.Galaxy;

/// <summary>The galaxy: stars with their planets and wormholes.</summary>
public sealed class Universe
{
    public Universe(IReadOnlyList<Star> stars)
    {
        Stars = stars;
    }

    public IReadOnlyList<Star> Stars { get; }

    /// <summary>Wormhole jumps between two stars.</summary>
    public int Hops(int from, int to)
    {
        return Search(from)[to].Distance;
    }

    /// <summary>
    /// The stars from one to another, both included. The wormholes form a
    /// tree, so this is the only path ( caches it per pair).
    /// </summary>
    public List<int> Route(int from, int to)
    {
        var steps = Search(from);
        var route = new List<int>();
        for (var s = to; s != -1; s = steps[s].Previous)
        {
            route.Insert(0, s);
        }

        return route;
    }

    private (int Distance, int Previous)[] Search(int from)
    {
        var steps = new (int Distance, int Previous)[Stars.Count];
        Array.Fill(steps, (-1, -1));
        steps[from] = (0, -1);
        var queue = new Queue<int>([from]);
        while (queue.TryDequeue(out var s))
        {
            foreach (var w in Stars[s].Wormholes)
            {
                // Every end is linked: the wormholes form a spanning tree.
                var next = w.Partner!.Star;
                if (steps[next].Distance < 0)
                {
                    steps[next] = (steps[s].Distance + 1, s);
                    queue.Enqueue(next);
                }
            }
        }

        return steps;
    }

    /// <summary>Adds everything that defines the galaxy to a state hash.</summary>
    public void AddTo(StateHash hash)
    {
        hash.Add(Stars.Count);
        foreach (var s in Stars)
        {
            hash.Add(s.ObjectId).Add(s.X).Add(s.Y).Add(s.Type).Add(s.Name);
            hash.Add(s.Planets.Count);
            foreach (var p in s.Planets)
            {
                hash.Add(p.ObjectId).Add(p.AnimationFrame).Add(p.Slot).Add(p.Name).Add(p.Temperature).Add(p.Type).Add(p.Size);
                hash.Add(p.Rating).Add(p.Moons).Add(p.Class).Add(p.Score);

                // A mod's planet type only: an original galaxy keeps its hash.
                if (p.Kind != 0 || p.Breathing != 0)
                {
                    hash.Add(p.Kind).Add(p.Breathing);
                }

                hash.Add(p.Orbit.Radius).Add(p.Orbit.Angle).Add(p.Orbit.Speed).Add(p.Orbit.X).Add(p.Orbit.Y);
                foreach (var m in p.Satellites)
                {
                    hash.Add(m.Radius).Add(m.Angle).Add(m.Speed);
                }
            }

            hash.Add(s.Wormholes.Count);
            foreach (var w in s.Wormholes)
            {
                hash.Add(w.ObjectId).Add(w.AnimationFrame).Add(w.Partner?.ObjectId ?? -1).Add(w.Name).Add(w.X).Add(w.Y);
            }
        }
    }
}
