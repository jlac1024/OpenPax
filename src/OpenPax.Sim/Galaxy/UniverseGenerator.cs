namespace OpenPax.Sim.Galaxy;

/// <summary>
/// Builds a new galaxy from a seed and star count exactly as the original
/// ; see docs/sim/universe.md. Every draw from the game stream
/// happens in the original's order.
/// </summary>
public static class UniverseGenerator
{
    public const int MaxStars = 100;

    // With one star the original takes a placeholder edge from star 0 to
    // itself, creates two wormholes that never link, and then crashes
    // placing them (null partner). So at least two stars.
    public const int MinStars = 2;

    // Star positions stay inside 20..430 on a 450 square map, with a 30x30
    // box around each star clear of every earlier one.
    private const int MapDraw = 450;
    private const int MapMin = 20;
    private const int MapMax = 430;
    private const int StarBox = 30;

    // Planets orbit the star at the center of the system view.
    private const int SystemCenter = 220;
    private static readonly int[] _orbitRadius = [38, 64, 90, 116, 152, 178];
    private const int OrbitSlots = 6;
    private const int SatellitesPerPlanet = 18;
    private const int GasGiantType = 4;
    private const int GasGiantClass = 5;

    // Wormholes sit on a circle around the system view center.
    private const double WormholeRadius = 175.0;

    /// <param name="kinds">Planet types mods add (docs/modding/content.md), drawn alongside the original's; none gives the original's galaxy, draw for draw.</param>
    public static Universe Generate(uint seed, int starCount, NameTables names, GameRandom random, ObjectIds ids, IReadOnlyList<Setup.PlanetKind>? kinds = null)
    {
        if (starCount < MinStars || starCount > MaxStars)
        {
            throw new ArgumentOutOfRangeException(nameof(starCount));
        }

        var (xs, ys, types) = PlaceStars(seed, starCount, random);

        // Only the first 102 names can be drawn, without a reseed.
        var nameIndex = new int[starCount];
        var used = new bool[102];
        for (var i = 0; i < starCount; i++)
        {
            int n;
            do
            {
                n = random.Next(102);
            }
            while (used[n]);

            used[n] = true;
            nameIndex[i] = n;
        }

        // The star objects are all created before any planet, so stars take
        // the first object ids.
        var stars = new Star[starCount];
        for (var i = 0; i < starCount; i++)
        {
            stars[i] = new Star(i, ids.Take(), xs[i], ys[i], types[i], names.StarNames[nameIndex[i]]);
        }

        foreach (var star in stars)
        {
            CreatePlanets(star, names, random, ids, kinds ?? []);
        }

        CreateWormholes(stars, names, random, ids);
        return new Universe(stars);
    }

    /// <summary>
    /// The first two steps: star positions, then types after a reseed. The
    /// setup screen's preview runs the same steps for 100 stars and shows the
    /// first ones, so it shows the real galaxy.
    /// </summary>
    public static (int[] X, int[] Y, int[] Types) PlaceStars(uint seed, int starCount, GameRandom random)
    {
        random.Seed(seed);
        var xs = new int[starCount];
        var ys = new int[starCount];
        for (var i = 0; i < starCount; i++)
        {
            do
            {
                xs[i] = random.Next(MapDraw);
                ys[i] = random.Next(MapDraw);
            }
            while (!Fits(xs, ys, i));
        }

        random.Seed(seed);
        var types = new int[starCount];
        for (var i = 0; i < starCount; i++)
        {
            types[i] = random.Next(7);
        }

        return (xs, ys, types);
    }

    private static bool Fits(int[] xs, int[] ys, int i)
    {
        if (xs[i] < MapMin || xs[i] > MapMax || ys[i] < MapMin || ys[i] > MapMax)
        {
            return false;
        }

        for (var j = 0; j < i; j++)
        {
            if (Math.Abs(xs[i] - xs[j]) < StarBox && Math.Abs(ys[i] - ys[j]) < StarBox)
            {
                return false;
            }
        }

        return true;
    }

    private static void CreatePlanets(Star star, NameTables names, GameRandom random, ObjectIds ids, IReadOnlyList<Setup.PlanetKind> kinds)
    {
        var count = random.Next(3) + random.Next(3) + 2;

        // Each new planet goes on the front of the star's list, so the last
        // one created ends up in the innermost occupied slot.
        var created = new List<Planet>(count);
        for (var i = 0; i < count; i++)
        {
            created.Insert(0, NewPlanet(random, ids));
        }

        var occupied = new bool[OrbitSlots];
        for (var i = 0; i < count; i++)
        {
            int slot;
            do
            {
                slot = random.Next(OrbitSlots);
            }
            while (occupied[slot]);

            occupied[slot] = true;
        }

        var next = 0;
        var allGasGiants = true;
        for (var slot = 0; slot < OrbitSlots; slot++)
        {
            if (!occupied[slot])
            {
                continue;
            }

            var planet = created[next++];

            // A system must not be all gas giants: the last planet may not
            // become one when every earlier planet did.
            var force = allGasGiants && next == count;
            InitPlanet(planet, star, slot, force, names, random, kinds);
            if (planet.Class != GasGiantClass)
            {
                allGasGiants = false;
            }

            star.Planets.Add(planet);
        }
    }

    private static Planet NewPlanet(GameRandom random, ObjectIds ids)
    {
        var id = ids.Take();
        var frame = random.Next(20);
        var satellites = new Satellite[SatellitesPerPlanet];
        for (var m = 0; m < SatellitesPerPlanet; m++)
        {
            var radius = random.Next(5) + 20;
            var angle = random.Next(3600) * 0.1;
            var speed = 25.0 / radius;
            if (random.Next(100) < 30)
            {
                speed = -speed;
            }

            satellites[m] = new Satellite(radius, angle, speed);
        }

        return new Planet(id, frame, satellites);
    }

    private static void InitPlanet(Planet planet, Star star, int slot, bool force, NameTables names, GameRandom random, IReadOnlyList<Setup.PlanetKind> kinds)
    {
        planet.Slot = slot;
        planet.Name = star.Name + " " + names.OrbitNumerals[slot];
        planet.Temperature = (star.Type - slot + 3) * 25;

        // Cold planets may be gas giants; when that draw says no (or the
        // planet is warm), a second draw picks type 1 to 3.
        int type;
        if (planet.Temperature < 1 && random.Next(2) != 0)
        {
            type = GasGiantType;
        }
        else
        {
            type = random.Next(3) + 1;
        }

        if (type == GasGiantType && force)
        {
            type = 3;
        }

        planet.Type = type;
        planet.Size = type == GasGiantType ? 53 : 40;
        planet.Rating = random.Next(5) + 1;
        planet.Moons = random.Next(planet.Type);
        if (planet.Type == GasGiantType)
        {
            planet.Class = GasGiantClass;
        }
        else if (kinds.Count == 0)
        {
            planet.Class = random.Next(5);
        }
        else
        {
            DrawClass(planet, random, kinds);
        }

        planet.Orbit = Orbit.Draw(random, SystemCenter, SystemCenter, _orbitRadius[slot], 50.0);
        planet.Orbit.Step();

        var sum = 0.0;
        for (var i = 0; i < planet.Moons; i++)
        {
            sum = random.Next(3) + sum;
        }

        planet.Score = (random.Next(5) + 1) + sum;
    }

    /// <summary>
    /// The port's own: a rocky planet's class drawn among the original's five
    /// and the mods' planet types, each original class ten parts and each
    /// type ten parts per unit of its weight (one draw, as the original's).
    /// </summary>
    private static void DrawClass(Planet planet, GameRandom random, IReadOnlyList<Setup.PlanetKind> kinds)
    {
        var parts = kinds.Select(k => (int)Math.Round(k.Weight * 10)).ToArray();
        var draw = random.Next(50 + parts.Sum());
        if (draw < 50)
        {
            planet.Class = draw / 10;
            return;
        }

        draw -= 50;
        for (var i = 0; i < kinds.Count; i++)
        {
            if (draw < parts[i])
            {
                planet.Class = kinds[i].Like;
                planet.Kind = i + 1;
                planet.Breathing = kinds[i].Breathing;
                planet.Rating += kinds[i].RatingBonus;
                return;
            }

            draw -= parts[i];
        }
    }

    private static void CreateWormholes(Star[] stars, NameTables names, GameRandom random, ObjectIds ids)
    {
        var n = stars.Length;
        var edges = new List<(int A, int B, int Distance)>(n * (n - 1) / 2);
        for (var i = 0; i < n; i++)
        {
            for (var j = i + 1; j < n; j++)
            {
                var dx = stars[i].X - stars[j].X;
                var dy = stars[i].Y - stars[j].Y;
                edges.Add((i, j, Numerics.X87.ToInt(Math.Sqrt(dx * dx + dy * dy))));
            }
        }

        var sorted = edges.ToArray();
        MsvcSort.Sort<(int A, int B, int Distance)>(sorted, (x, y) => x.Distance.CompareTo(y.Distance));

        // A spanning tree: the shortest edge, then repeatedly the first edge
        // in sorted order that reaches exactly one new star, scanning from
        // the second edge each time.
        var taken = new bool[sorted.Length];
        var connected = new bool[n];
        var degree = new int[n];
        if (sorted.Length > 0)
        {
            Take(0);
            var e = 1;
            while (e < sorted.Length)
            {
                if (connected[sorted[e].A] != connected[sorted[e].B])
                {
                    Take(e);
                    e = 1;
                }
                else
                {
                    e++;
                }
            }
        }

        // Every star gets as many wormholes as tree edges touch it, created
        // star by star; each draws its starting animation frame.
        foreach (var star in stars)
        {
            for (var k = 0; k < degree[star.Index]; k++)
            {
                star.Wormholes.Add(new Wormhole(ids.Take(), random.Next(45), star.Index));
            }
        }

        // Link in sorted order: the lower-numbered star's ends are used from
        // the last one backwards, the other star's first free end forwards.
        var remaining = (int[])degree.Clone();
        for (var e = 0; e < sorted.Length; e++)
        {
            if (!taken[e])
            {
                continue;
            }

            var from = stars[sorted[e].A];
            var to = stars[sorted[e].B];
            Link(from, remaining[from.Index] - 1, to, names);
            remaining[from.Index]--;
        }

        foreach (var star in stars)
        {
            foreach (var w in star.Wormholes)
            {
                Place(star, w, stars);
            }
        }

        void Take(int e)
        {
            taken[e] = true;
            connected[sorted[e].A] = connected[sorted[e].B] = true;
            degree[sorted[e].A]++;
            degree[sorted[e].B]++;
        }
    }

    private static void Link(Star from, int index, Star to, NameTables names)
    {
        // Stars already linked are left alone; a tree never has that case.
        if (from.Wormholes.Any(w => w.Partner is { } p && p.Star == to.Index))
        {
            return;
        }

        var end = to.Wormholes.FirstOrDefault(w => w.Partner is null)
            ?? throw new InvalidOperationException("No free wormhole end to link to");
        var start = from.Wormholes[index];
        start.Partner = end;
        start.Name = names.JumpPrefix + to.Name;
        end.Partner = start;
        end.Name = names.JumpPrefix + from.Name;
    }

    private static void Place(Star star, Wormhole w, Star[] stars)
    {
        // A tree over two or more stars links every end. An unlinked end
        // would crash the original here (it follows a null partner).
        var target = w.Partner is { } p ? stars[p.Star] : star;
        var degrees = Numerics.X87.DirectionDegrees(target.X - star.X, target.Y - star.Y);
        var rad = Numerics.X87.DegreesToRadians(degrees);
        w.X = SystemCenter + Numerics.X87.ToInt(Numerics.X87.CosTimes(rad, WormholeRadius));
        w.Y = SystemCenter + Numerics.X87.ToInt(Numerics.X87.SinTimes(rad, WormholeRadius));
    }
}
