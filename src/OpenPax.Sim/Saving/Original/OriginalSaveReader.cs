using OpenPax.Sim.Galaxy;
using OpenPax.Sim.Players;
using OpenPax.Sim.Setup;

namespace OpenPax.Sim.Saving.Original;

/// <summary>
/// Reads an original single player save into a port game, block by block
/// in the original's order (docs/formats/saves.md). Universe objects are
/// found by object id and players by index once everything is read, as the
/// original relinks them.
/// </summary>
internal sealed partial class OriginalSaveReader
{
    private const int HumanTag = 0x382;
    private const int ComputerTag = 0x3DD;
    private const int SatellitesPerPlanet = 18;
    private const int KnownTechBits = 900;

    private readonly OriginalStream _in;
    private readonly GameTables _tables;
    private readonly IComputerPlayer? _computerPlayers;
    private readonly Dictionary<int, ISpaceObject> _objects = [];
    private readonly List<Action> _relinks = [];
    private readonly Dictionary<int, int> _planetStars = [];
    private GameRandom _random = new(0);

    public OriginalSaveReader(Stream stream, GameTables tables, IComputerPlayer? computerPlayers)
    {
        _in = new OriginalStream(stream);
        _tables = tables;
        _computerPlayers = computerPlayers;
    }

    /// <summary>The object picked in the original's view (universe +0x9C58), by object id; -1 for none.</summary>
    public int SelectionId { get; private set; } = -1;

    /// <summary>The star in view (universe +0x9C54).</summary>
    public int CurrentStar { get; private set; }

    /// <summary>The header: the mark, the single player magic and version 0x430.</summary>
    public bool ReadHeader()
    {
        return _in.ReadMark() && (uint)_in.Int32() == OriginalSave.SinglePlayerMagic && _in.Int32() == OriginalSave.Version;
    }

    public Game ReadGame()
    {
        _in.Byte();
        var secondaryIds = _in.Int32();
        _in.Int32();

        var seed = (uint)_in.Int32();
        var difficulty = _in.Int32();

        // The original keeps no random state: the port starts the stream
        // again from the saved seed, then makes the draws loading makes.
        _random = new GameRandom(seed);
        var year = _in.Int32();
        var step = _in.Byte();
        _in.End();

        var universe = ReadUniverse();
        _in.Byte();
        var over = _in.Bool();

        var count = _in.Int32();
        var players = new List<Player>();
        var humans = new List<HumanPlayerSetup>();
        for (var i = 0; i < count; i++)
        {
            var tag = _in.Int32();
            var player = ReadPlayer(tag == ComputerTag);
            players.Add(player);
            _players.Add(player);
            _in.End();
            if (!player.IsAi)
            {
                humans.Add(new HumanPlayerSetup(player.Name, player.Flag, player.FleetColor, player.Species));
            }
        }

        _in.Byte();
        _in.End();
        var nextObjectId = _in.Int32();
        _in.End();

        var setup = new GameSetup(seed, universe.Stars.Count, players.Count, difficulty, humans);
        var game = new Game(setup, _tables, _random) { ComputerPlayers = _computerPlayers };
        game.Universe = universe;
        game.Players.AddRange(players);
        foreach (var relink in _relinks)
        {
            relink();
        }

        Settle(game, players, over);
        game.Calendar.Restore(year, step);
        game.ObjectIds.Restore(nextObjectId);
        game.SecondaryIds.Restore(secondaryIds);
        return game;
    }

    /// <summary>The fields every universe object writes first.</summary>
    private readonly record struct SpaceObject(int X, int Y, string Name, int Id, int CenterId, int Star, int Radius, double Angle, double Speed);

    private SpaceObject ReadSpaceObject()
    {
        var x = _in.Int32();
        var y = _in.Int32();
        for (var i = 0; i < 4; i++)
        {
            _in.Int32();
        }

        var name = _in.String();
        var id = _in.Int32();
        _in.Double();
        _in.Double();
        _in.Int32();
        var center = _in.Int32();
        var star = _in.Int32();
        var radius = _in.Int32();
        var angle = _in.Double();
        var speed = _in.Double();
        _in.End();
        return new SpaceObject(x, y, name, id, center, star, radius, angle, speed);
    }

    private Universe ReadUniverse()
    {
        _in.Int32();
        var count = _in.Int32();
        var stars = new List<Star>();
        for (var i = 0; i < count; i++)
        {
            stars.Add(ReadStar());
        }

        CurrentStar = _in.Int32();
        SelectionId = _in.Int32();
        _in.End();
        return new Universe(stars);
    }

    private Star ReadStar()
    {
        for (var i = 0; i < 4; i++)
        {
            _in.Int32();
        }

        var x = _in.Int32();
        var y = _in.Int32();

        // The star's center object carries its name and id.
        var center = ReadSpaceObject();
        var type = _in.Int32();
        _in.End();

        var planets = new List<Planet>();
        var planetCount = _in.Int32();
        for (var i = 0; i < planetCount; i++)
        {
            planets.Add(ReadPlanet());
        }

        var holes = new List<(Wormhole Hole, int Partner)>();
        var holeCount = _in.Int32();
        for (var i = 0; i < holeCount; i++)
        {
            holes.Add(ReadWormhole());
        }

        var sight = new int[Player.MaxPlayers];
        for (var i = 0; i < sight.Length; i++)
        {
            sight[i] = _in.Int32();
        }

        var index = _in.Int32();
        _in.End();

        var star = new Star(index, center.Id, x, y, type, center.Name);
        _objects[center.Id] = star.Center;
        foreach (var planet in planets)
        {
            _planetStars[planet.ObjectId] = index;
        }

        star.Planets.AddRange(planets);
        sight.CopyTo(star.Sight, 0);
        foreach (var (hole, partner) in holes)
        {
            star.Wormholes.Add(hole);
            _relinks.Add(() => hole.Partner = partner >= 0 && _objects.TryGetValue(partner, out var p) ? p as Wormhole : null);
        }

        return star;
    }

    /// <summary>
    /// A planet. Its constructor makes the same draws as a new
    /// planet: the animation frame and each satellite's three;
    /// the save then gives back the satellites' radius and speed but not
    /// their angles, which stay as drawn (a quirk kept).
    /// </summary>
    private Planet ReadPlanet()
    {
        var frame = _random.Next(20);
        var angles = new double[SatellitesPerPlanet];
        for (var m = 0; m < SatellitesPerPlanet; m++)
        {
            _random.Next(5);
            angles[m] = _random.Next(3600) * 0.1;
            _random.Next(100);
        }

        var o = ReadSpaceObject();
        var temperature = _in.Int32();
        var planetClass = _in.Int32();
        var moons = _in.Int32();
        var size = _in.Int32();
        var type = _in.Int32();
        var rating = _in.Int32();
        var score = _in.Double();
        var slot = _in.Int32() - 1;
        var satellites = new Satellite[SatellitesPerPlanet];
        for (var m = 0; m < SatellitesPerPlanet; m++)
        {
            var radius = _in.Int32();
            satellites[m] = new Satellite(radius, angles[m], _in.Double());
        }

        _in.End();
        var planet = new Planet(o.Id, frame, satellites)
        {
            Name = o.Name,
            Temperature = temperature,
            Class = planetClass,
            Moons = moons,
            Size = size,
            Type = type,
            Rating = rating,
            Score = score,
            Slot = slot,
            Orbit = new Orbit(StarCenter.Position, StarCenter.Position, o.Radius, o.Angle, o.Speed),
        };
        planet.Orbit.RestorePosition(o.X, o.Y);
        _objects[o.Id] = planet;
        return planet;
    }

    /// <summary>A wormhole ( draws its frame). Quirk kept: whether it is known is not saved.</summary>
    private (Wormhole, int Partner) ReadWormhole()
    {
        var frame = _random.Next(45);
        var o = ReadSpaceObject();
        var partner = _in.Int32();
        var star = _in.Int32();
        _in.End();
        var hole = new Wormhole(o.Id, frame, star) { Name = o.Name, X = o.X, Y = o.Y };
        _objects[o.Id] = hole;
        return (hole, partner);
    }

    private ISpaceObject? Find(int id) => id >= 0 && _objects.TryGetValue(id, out var o) ? o : null;

    /// <summary>
    /// After the relinks: fleets go into their systems, each at
    /// the front, in player order then fleet order, so a system's order is
    /// not the one saved (a quirk kept). What the original does not save
    /// starts afresh: the tasks' countdowns, the computer players' patience,
    /// battles. The port's own counters are set from what is saved.
    /// </summary>
    private void Settle(Game game, List<Player> players, bool over)
    {
        foreach (var player in players)
        {
            foreach (var fleet in player.Fleets)
            {
                game.Universe.Stars[fleet.Star].Fleets.Insert(0, fleet);
            }

            // The original keeps no home star; the oldest colony stands in.
            if (player.Colonies.Count > 0)
            {
                var home = player.Colonies[^1];
                player.HomeStar = home.Star;
                player.HomePlanetSlot = home.Planet.Slot;
            }
        }

        game.Over = over;
        game.NextMissionId = _nextMission;
    }
}
