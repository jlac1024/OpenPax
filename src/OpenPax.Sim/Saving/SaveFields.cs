using System.Reflection;

namespace OpenPax.Sim.Saving;

/// <summary>
/// The fields a type saves: every instance field of it and its bases, by a
/// name that stays unique across the chain, in name order so files come out
/// the same every time. References to the game's computer player module are
/// not saved; the loader puts the running one back. Fields marked
/// [NotSaved] (a recording of the game) are not saved. Each writer and
/// reader keeps its own (the simulation has no static state).
/// </summary>
internal sealed class SaveFields
{
    private const BindingFlags Instance = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly;

    private readonly Dictionary<Type, (string Name, FieldInfo Field)[]> _cache = [];

    public (string Name, FieldInfo Field)[] Of(Type type)
    {
        if (_cache.TryGetValue(type, out var known))
        {
            return known;
        }

        var fields = new List<(string, FieldInfo)>();
        for (var t = type; t is not null && t != typeof(object) && t != typeof(ValueType); t = t.BaseType)
        {
            foreach (var f in t.GetFields(Instance))
            {
                if (f.FieldType != typeof(IComputerPlayer) && !f.IsDefined(typeof(NotSavedAttribute)))
                {
                    fields.Add((t == type ? f.Name : t.Name + "." + f.Name, f));
                }
            }
        }

        var result = fields.OrderBy(f => f.Item1, StringComparer.Ordinal).ToArray();
        _cache[type] = result;
        return result;
    }
}
