using System.Text;

namespace OpenPax.Host.Update.DBus;

/// <summary>
/// D-Bus values read from a message by their signature: numbers as long or
/// double, strings and object paths as string, arrays as lists, a{..}
/// dictionaries as Dictionary&lt;string, object&gt;, structs as object arrays,
/// variants as their value. Alignment counts from the start of the message.
/// </summary>
internal sealed class DBusReader(byte[] data, int start)
{
    private int _at = start;

    public int Position => _at;

    public void Pad(int alignment)
    {
        while (_at % alignment != 0)
        {
            _at++;
        }
    }

    public byte Byte() => data[_at++];

    public uint UInt32()
    {
        Pad(4);
        var value = BitConverter.ToUInt32(data, _at);
        _at += 4;
        return value;
    }

    public string String()
    {
        var length = (int)UInt32();
        var text = Encoding.UTF8.GetString(data, _at, length);
        _at += length + 1;
        return text;
    }

    public string Signature()
    {
        var length = data[_at++];
        var text = Encoding.ASCII.GetString(data, _at, length);
        _at += length + 1;
        return text;
    }

    /// <summary>The values of a body by its whole signature.</summary>
    public List<object> Values(string signature)
    {
        var values = new List<object>();
        var i = 0;
        while (i < signature.Length)
        {
            values.Add(Value(signature, ref i));
        }

        return values;
    }

    private object Value(string signature, ref int i)
    {
        var code = signature[i++];
        switch (code)
        {
            case 'y':
                return (long)Byte();
            case 'b':
                return UInt32() != 0;
            case 'n':
                Pad(2);
                _at += 2;
                return (long)BitConverter.ToInt16(data, _at - 2);
            case 'q':
                Pad(2);
                _at += 2;
                return (long)BitConverter.ToUInt16(data, _at - 2);
            case 'i':
                return (long)(int)UInt32();
            case 'u' or 'h':
                return (long)UInt32();
            case 'x' or 't':
                Pad(8);
                _at += 8;
                return BitConverter.ToInt64(data, _at - 8);
            case 'd':
                Pad(8);
                _at += 8;
                return BitConverter.ToDouble(data, _at - 8);
            case 's' or 'o':
                return String();
            case 'g':
                return Signature();
            case 'v':
                {
                    var inner = Signature();
                    var j = 0;
                    return Value(inner, ref j);
                }

            case '(':
                {
                    Pad(8);
                    var items = new List<object>();
                    while (signature[i] != ')')
                    {
                        items.Add(Value(signature, ref i));
                    }

                    i++;
                    return items.ToArray();
                }

            case 'a':
                return Array(signature, ref i);
            default:
                throw new InvalidDataException("D-Bus type '" + code + "' is not read here");
        }
    }

    private object Array(string signature, ref int i)
    {
        var length = (int)UInt32();
        var element = ElementEnd(signature, i);
        var itemSignature = signature[i..element];
        i = element;
        var dictionary = itemSignature.StartsWith('{');
        Pad(itemSignature[0] is '{' or '(' or 'x' or 't' or 'd' ? 8 : itemSignature[0] is 'n' or 'q' ? 2 : itemSignature[0] is 'y' or 'g' or 'v' ? 1 : 4);
        var end = _at + length;
        if (dictionary)
        {
            var entries = new Dictionary<string, object>(StringComparer.Ordinal);
            var pair = itemSignature[1..^1];
            while (_at < end)
            {
                Pad(8);
                var j = 0;
                var key = Value(pair, ref j);
                var value = Value(pair, ref j);
                entries[Convert.ToString(key, System.Globalization.CultureInfo.InvariantCulture) ?? ""] = value;
            }

            return entries;
        }

        var list = new List<object>();
        while (_at < end)
        {
            var j = 0;
            list.Add(Value(itemSignature, ref j));
        }

        return list;
    }

    // Where a single complete type starting at i ends.
    private static int ElementEnd(string signature, int i)
    {
        var c = signature[i];
        if (c == 'a')
        {
            return ElementEnd(signature, i + 1);
        }

        if (c is '(' or '{')
        {
            var close = c == '(' ? ')' : '}';
            var depth = 0;
            for (var k = i; k < signature.Length; k++)
            {
                if (signature[k] == c)
                {
                    depth++;
                }
                else if (signature[k] == close && --depth == 0)
                {
                    return k + 1;
                }
            }

            throw new InvalidDataException("unbalanced D-Bus signature " + signature);
        }

        return i + 1;
    }
}
