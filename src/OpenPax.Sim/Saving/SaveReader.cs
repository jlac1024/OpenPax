using System.Collections;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text;
using OpenPax.Sim.Setup;

namespace OpenPax.Sim.Saving;

/// <summary>
/// Reads what <see cref="SaveWriter"/> wrote. Objects are made empty and
/// numbered before their fields are read, so later mentions find them.
/// Fields are matched by name: one the save lacks keeps its empty value,
/// one the type lacks is read and dropped. The computer player module is
/// not in the save; the one given is put back.
/// </summary>
internal sealed class SaveReader
{
    private readonly BinaryReader _in;
    private readonly SaveTables _tables;
    private readonly SaveTypes _types;
    private readonly SaveFields _fields = new();
    private readonly IComputerPlayer? _computerPlayers;
    private readonly List<object> _objects = [];
    private readonly List<string> _names = [];

    public SaveReader(Stream stream, GameTables tables, IComputerPlayer? computerPlayers, IEnumerable<Assembly> assemblies)
    {
        _in = new BinaryReader(stream, Encoding.UTF8, leaveOpen: true);
        _tables = new SaveTables(tables);
        _types = new SaveTypes(assemblies);
        _computerPlayers = computerPlayers;
    }

    /// <summary>Checks the file is a save and returns its version.</summary>
    public int ReadHeader()
    {
        if (_in.ReadUInt32() != SaveFile.Magic)
        {
            throw new InvalidDataException(Strings.SaveNotOurs);
        }

        return _in.ReadInt32();
    }

    private string Name()
    {
        var number = _in.ReadInt32();
        if (number == _names.Count)
        {
            _names.Add(Spelling.Current(_in.ReadString()));
        }

        return _names[number];
    }

    private Type TypeName() => _types.Find(Name());

    public object? Value()
    {
        var tag = (SaveTag)_in.ReadByte();
        switch (tag)
        {
            case SaveTag.Null:
                return null;
            case SaveTag.Bool:
                return _in.ReadBoolean();
            case SaveTag.Byte:
                return _in.ReadByte();
            case SaveTag.SByte:
                return _in.ReadSByte();
            case SaveTag.Int16:
                return _in.ReadInt16();
            case SaveTag.UInt16:
                return _in.ReadUInt16();
            case SaveTag.Int32:
                return _in.ReadInt32();
            case SaveTag.UInt32:
                return _in.ReadUInt32();
            case SaveTag.Int64:
                return _in.ReadInt64();
            case SaveTag.UInt64:
                return _in.ReadUInt64();
            case SaveTag.Double:
                return _in.ReadDouble();
            case SaveTag.Single:
                return _in.ReadSingle();
            case SaveTag.Char:
                return (char)_in.ReadUInt16();
            case SaveTag.String:
                return _in.ReadString();
            case SaveTag.Enum:
                {
                    var type = TypeName();
                    return Enum.ToObject(type, _in.ReadInt64());
                }

            case SaveTag.Table:
                {
                    var kind = _in.ReadInt32();
                    return _tables.Find(kind, _in.ReadInt32());
                }

            case SaveTag.Reference:
                return _objects[_in.ReadInt32()];
            case SaveTag.Struct:
                {
                    var type = TypeName();
                    var boxed = RuntimeHelpers.GetUninitializedObject(type);
                    Fields(boxed, type);
                    return boxed;
                }

            case SaveTag.Array:
                {
                    var element = TypeName();
                    var array = Array.CreateInstance(element, _in.ReadInt32());
                    _objects.Add(array);
                    for (var i = 0; i < array.Length; i++)
                    {
                        array.SetValue(Value(), i);
                    }

                    return array;
                }

            case SaveTag.Dictionary:
                {
                    var type = TypeName();
                    var dictionary = (IDictionary)(Activator.CreateInstance(type) ?? throw new InvalidDataException(string.Format(Strings.SaveUnknownType, type)));
                    _objects.Add(dictionary);
                    var count = _in.ReadInt32();
                    for (var i = 0; i < count; i++)
                    {
                        var key = Value() ?? throw new InvalidDataException(string.Format(Strings.SaveBadTag, "null key"));
                        dictionary.Add(key, Value());
                    }

                    return dictionary;
                }

            case SaveTag.Collection:
                {
                    var type = TypeName();
                    var collection = Activator.CreateInstance(type) ?? throw new InvalidDataException(string.Format(Strings.SaveUnknownType, type));
                    _objects.Add(collection);
                    var add = type.GetMethod("Add") ?? throw new InvalidDataException(string.Format(Strings.SaveUnknownType, type));
                    var count = _in.ReadInt32();
                    for (var i = 0; i < count; i++)
                    {
                        add.Invoke(collection, [Value()]);
                    }

                    return collection;
                }

            case SaveTag.Object:
                {
                    var type = TypeName();
                    var value = RuntimeHelpers.GetUninitializedObject(type);
                    _objects.Add(value);
                    Fields(value, type);
                    return value;
                }

            default:
                throw new InvalidDataException(string.Format(Strings.SaveBadTag, tag));
        }
    }

    private void Fields(object value, Type type)
    {
        var known = _fields.Of(type).ToDictionary(f => f.Name, f => f.Field);
        var count = _in.ReadInt32();
        for (var i = 0; i < count; i++)
        {
            var name = Name();
            var read = Value();
            if (known.TryGetValue(name, out var field))
            {
                field.SetValue(value, read);
            }
        }

        foreach (var field in type.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic))
        {
            if (field.FieldType == typeof(IComputerPlayer))
            {
                field.SetValue(value, _computerPlayers);
            }
        }
    }
}
