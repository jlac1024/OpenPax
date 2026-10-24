using System.Globalization;

namespace OpenPax.Lua.Runtime;

/// <summary>
/// One Lua value: nil, a boolean, an integer (64 bits) or a float, a string,
/// a table, a function or a host object. Integers and floats stay apart, as
/// in Lua 5.4; as a table key a float with a whole value is the integer.
/// </summary>
public readonly struct LuaValue : IEquatable<LuaValue>
{
    private readonly object? _ref;
    private readonly long _bits;
    private readonly Tag _tag;

    private LuaValue(Tag tag, long bits, object? reference)
    {
        _tag = tag;
        _bits = bits;
        _ref = reference;
    }

    private enum Tag : byte
    {
        Nil,
        False,
        True,
        Integer,
        Float,
        String,
        Table,
        Function,
        UserData,
    }

    public static LuaValue Nil => default;

    public static LuaValue True { get; } = new(Tag.True, 0, null);

    public static LuaValue False { get; } = new(Tag.False, 0, null);

    public LuaType Type => _tag switch
    {
        Tag.Nil => LuaType.Nil,
        Tag.False or Tag.True => LuaType.Boolean,
        Tag.Integer or Tag.Float => LuaType.Number,
        Tag.String => LuaType.String,
        Tag.Table => LuaType.Table,
        Tag.Function => LuaType.Function,
        _ => LuaType.UserData,
    };

    public bool IsNil => _tag == Tag.Nil;

    public bool IsInteger => _tag == Tag.Integer;

    public bool IsFloat => _tag == Tag.Float;

    public bool IsNumber => _tag is Tag.Integer or Tag.Float;

    public bool IsString => _tag == Tag.String;

    /// <summary>Everything but nil and false counts as true.</summary>
    public bool IsTruthy => _tag is not (Tag.Nil or Tag.False);

    public long AsInteger => _bits;

    public double AsFloat => _tag == Tag.Integer ? _bits : BitConverter.Int64BitsToDouble(_bits);

    public string AsString => (string)_ref!; // Only read once the tag says string.

    public LuaTable? AsTable => _ref as LuaTable;

    public LuaFunction? AsFunction => _ref as LuaFunction;

    public LuaUserData? AsUserData => _ref as LuaUserData;

    /// <summary>Text read as a Lua numeral (an integer or a float), or nil.</summary>
    public static LuaValue ParseNumber(string text) => Numbers.Parse(text);

    public static LuaValue Of(bool b) => b ? True : False;

    public static LuaValue Of(long i) => new(Tag.Integer, i, null);

    public static LuaValue Of(double d) => new(Tag.Float, BitConverter.DoubleToInt64Bits(d), null);

    public static LuaValue Of(string? s) => s is null ? Nil : new(Tag.String, 0, s);

    public static LuaValue Of(LuaTable? t) => t is null ? Nil : new(Tag.Table, 0, t);

    public static LuaValue Of(LuaFunction? f) => f is null ? Nil : new(Tag.Function, 0, f);

    public static LuaValue Of(LuaUserData? u) => u is null ? Nil : new(Tag.UserData, 0, u);

    public static implicit operator LuaValue(bool b) => Of(b);

    public static implicit operator LuaValue(long i) => Of(i);

    public static implicit operator LuaValue(double d) => Of(d);

    public static implicit operator LuaValue(string? s) => Of(s);

    public static implicit operator LuaValue(LuaTable? t) => Of(t);

    public static implicit operator LuaValue(LuaFunction? f) => Of(f);

    public static bool operator ==(LuaValue a, LuaValue b) => a.Equals(b);

    public static bool operator !=(LuaValue a, LuaValue b) => !a.Equals(b);

    /// <summary>The same as a table key: a float with a whole value becomes the integer.</summary>
    public LuaValue AsKey()
    {
        if (_tag == Tag.Float)
        {
            var d = AsFloat;
            if (Numbers.FloatToInteger(d) is { } i)
            {
                return Of(i);
            }
        }

        return this;
    }

    /// <summary>Raw equality: numbers by value (1 == 1.0), strings by text, the rest by identity.</summary>
    public bool Equals(LuaValue other)
    {
        if (_tag == other._tag)
        {
            return _tag switch
            {
                Tag.Nil or Tag.False or Tag.True => true,
                Tag.Integer => _bits == other._bits,
                Tag.Float => AsFloat == other.AsFloat,
                Tag.String => string.Equals((string)_ref!, (string)other._ref!, StringComparison.Ordinal), // Both strings by their tags.
                _ => ReferenceEquals(_ref, other._ref),
            };
        }

        return IsNumber && other.IsNumber && Numbers.Equal(this, other);
    }

    public override bool Equals(object? obj) => obj is LuaValue v && Equals(v);

    public override int GetHashCode() => _tag switch
    {
        Tag.Integer => _bits.GetHashCode(),
        Tag.Float => Numbers.FloatToInteger(AsFloat) is { } i ? i.GetHashCode() : AsFloat.GetHashCode(),
        Tag.String => StringComparer.Ordinal.GetHashCode((string)_ref!), // A string by its tag.
        Tag.Nil or Tag.False or Tag.True => (int)_tag,
        _ => System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(_ref!), // A reference by its tag.
    };

    /// <summary>As tostring shows it, without metatables.</summary>
    public override string ToString() => _tag switch
    {
        Tag.Nil => "nil",
        Tag.False => "false",
        Tag.True => "true",
        Tag.Integer => _bits.ToString(CultureInfo.InvariantCulture),
        Tag.Float => Numbers.FormatFloat(AsFloat),
        Tag.String => (string)_ref!, // A string by its tag.
        Tag.Table => "table",
        Tag.Function => "function",
        _ => "userdata",
    };
}
