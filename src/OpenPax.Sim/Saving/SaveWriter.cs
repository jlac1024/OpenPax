using System.Collections;
using System.Text;
using OpenPax.Sim.Setup;

namespace OpenPax.Sim.Saving;

/// <summary>
/// Writes an object graph as a saved game (docs/formats/port-saves.md):
/// every object once, by its fields' names, later mentions by number; game
/// data by its place in the tables. Little endian throughout.
/// </summary>
internal sealed class SaveWriter
{
    private readonly BinaryWriter _out;
    private readonly SaveTables _tables;
    private readonly SaveFields _fields = new();
    private readonly Dictionary<object, int> _objects = new(ReferenceEqualityComparer.Instance);
    private readonly Dictionary<string, int> _names = [];

    public SaveWriter(Stream stream, GameTables tables)
    {
        _out = new BinaryWriter(stream, Encoding.UTF8, leaveOpen: true);
        _tables = new SaveTables(tables);
    }

    public void WriteHeader(int version)
    {
        _out.Write(SaveFile.Magic);
        _out.Write(version);
    }

    public void Flush() => _out.Flush();

    /// <summary>A name (a type or a field) as a number, spelled out the first time.</summary>
    private void Name(string name)
    {
        if (_names.TryGetValue(name, out var number))
        {
            _out.Write(number);
            return;
        }

        number = _names.Count;
        _names[name] = number;
        _out.Write(number);
        _out.Write(name);
    }

    private void TypeName(Type type) => Name(SaveTypes.NameOf(type));

    public void Value(object? value)
    {
        switch (value)
        {
            case null:
                Tag(SaveTag.Null);
                return;
            case bool b:
                Tag(SaveTag.Bool);
                _out.Write(b);
                return;
            case byte b:
                Tag(SaveTag.Byte);
                _out.Write(b);
                return;
            case sbyte b:
                Tag(SaveTag.SByte);
                _out.Write(b);
                return;
            case short s:
                Tag(SaveTag.Int16);
                _out.Write(s);
                return;
            case ushort s:
                Tag(SaveTag.UInt16);
                _out.Write(s);
                return;
            case int i:
                Tag(SaveTag.Int32);
                _out.Write(i);
                return;
            case uint i:
                Tag(SaveTag.UInt32);
                _out.Write(i);
                return;
            case long l:
                Tag(SaveTag.Int64);
                _out.Write(l);
                return;
            case ulong l:
                Tag(SaveTag.UInt64);
                _out.Write(l);
                return;
            case double d:
                Tag(SaveTag.Double);
                _out.Write(d);
                return;
            case float f:
                Tag(SaveTag.Single);
                _out.Write(f);
                return;
            case char c:
                Tag(SaveTag.Char);
                _out.Write((ushort)c);
                return;
            case string s:
                Tag(SaveTag.String);
                _out.Write(s);
                return;
            case Enum e:
                Tag(SaveTag.Enum);
                TypeName(e.GetType());
                _out.Write(Convert.ToInt64(e, System.Globalization.CultureInfo.InvariantCulture));
                return;
        }

        var type = value.GetType();
        if (type.IsValueType)
        {
            Tag(SaveTag.Struct);
            TypeName(type);
            Fields(value, type);
            return;
        }

        if (_tables.TryPlace(value, out var place))
        {
            Tag(SaveTag.Table);
            _out.Write(place.Kind);
            _out.Write(place.Index);
            return;
        }

        if (_objects.TryGetValue(value, out var number))
        {
            Tag(SaveTag.Reference);
            _out.Write(number);
            return;
        }

        if (value is Delegate)
        {
            throw new NotSupportedException(string.Format(Strings.SaveCannotWrite, type));
        }

        // A list the compiler made for a collection expression (a hidden
        // type of whatever assembly wrote it) is saved as a plain array,
        // which a read-only list field takes just as well.
        if (type.Name.StartsWith("<>", StringComparison.Ordinal) && value is IEnumerable items && ElementOf(type) is { } element)
        {
            var copy = items.Cast<object?>().ToArray();
            var array = Array.CreateInstance(element, copy.Length);
            for (var i = 0; i < copy.Length; i++)
            {
                array.SetValue(copy[i], i);
            }

            value = array;
            type = array.GetType();
        }

        _objects[value] = _objects.Count;
        switch (value)
        {
            case Array array:
                Tag(SaveTag.Array);
                TypeName(type.GetElementType()!); // an array type always has one
                _out.Write(array.Length);
                foreach (var item in array)
                {
                    Value(item);
                }

                return;
            case IDictionary dictionary:
                Tag(SaveTag.Dictionary);
                TypeName(type);
                _out.Write(dictionary.Count);
                foreach (DictionaryEntry entry in dictionary)
                {
                    Value(entry.Key);
                    Value(entry.Value);
                }

                return;
            case ICollection collection when SaveTypes.IsCollection(type):
                Tag(SaveTag.Collection);
                TypeName(type);
                _out.Write(collection.Count);
                foreach (var item in collection)
                {
                    Value(item);
                }

                return;
            default:
                Tag(SaveTag.Object);
                TypeName(type);
                Fields(value, type);
                return;
        }
    }

    private static Type? ElementOf(Type type)
    {
        return type.GetInterfaces().FirstOrDefault(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IEnumerable<>))?.GetGenericArguments()[0];
    }

    private void Fields(object value, Type type)
    {
        var fields = _fields.Of(type);
        _out.Write(fields.Length);
        foreach (var (name, field) in fields)
        {
            Name(name);
            Value(field.GetValue(value));
        }
    }

    private void Tag(SaveTag tag) => _out.Write((byte)tag);
}
