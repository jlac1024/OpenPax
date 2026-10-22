using OpenPax.Sim.Setup;

namespace OpenPax.Sim.Saving;

/// <summary>
/// The game data a save refers to rather than holds: the tables themselves,
/// each species, tech and hull, and the galaxy names, found again by their
/// place in the tables when the game is loaded.
/// </summary>
internal sealed class SaveTables
{
    private readonly Dictionary<object, (int Kind, int Index)> _places = new(ReferenceEqualityComparer.Instance);
    private readonly GameTables _tables;

    public SaveTables(GameTables tables)
    {
        _tables = tables;
        _places[tables] = (0, 0);
        _places[tables.GalaxyNames] = (1, 0);
        for (var i = 0; i < tables.Species.Count; i++)
        {
            _places.TryAdd(tables.Species[i], (2, i));
        }

        for (var i = 0; i < tables.Techs.Count; i++)
        {
            _places.TryAdd(tables.Techs[i], (3, i));
        }

        for (var i = 0; i < tables.Hulls.Count; i++)
        {
            _places.TryAdd(tables.Hulls[i], (4, i));
        }
    }

    public bool TryPlace(object value, out (int Kind, int Index) place) => _places.TryGetValue(value, out place);

    public object Find(int kind, int index)
    {
        return kind switch
        {
            0 => _tables,
            1 => _tables.GalaxyNames,
            2 => _tables.Species[index],
            3 => _tables.Techs[index],
            4 => _tables.Hulls[index],
            _ => throw new InvalidDataException(string.Format(Strings.SaveBadTable, kind)),
        };
    }
}
