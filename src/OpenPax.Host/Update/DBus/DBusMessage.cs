namespace OpenPax.Host.Update.DBus;

/// <summary>A D-Bus message as received: its kind, where it comes from and goes, and its body's values.</summary>
internal sealed record DBusMessage(DBusMessage.Kinds Kind, uint Serial, uint ReplySerial, string Path, string Interface, string Member, string ErrorName, List<object> Body)
{
    /// <summary>The message types of the D-Bus specification.</summary>
    public enum Kinds : byte
    {
        Invalid = 0,
        MethodCall = 1,
        MethodReturn = 2,
        Error = 3,
        Signal = 4,
    }

    /// <summary>Reads one message from its whole bytes (header, padding, body).</summary>
    public static DBusMessage Read(byte[] data)
    {
        if (data[0] != (byte)'l')
        {
            throw new InvalidDataException("big-endian D-Bus messages are not read here");
        }

        var reader = new DBusReader(data, 1);
        var kind = (Kinds)reader.Byte();
        reader.Byte();
        reader.Byte();
        var bodyLength = (int)reader.UInt32();
        var serial = reader.UInt32();
        var fields = (List<object>)reader.Values("a(yv)")[0];
        uint replySerial = 0;
        string path = "", iface = "", member = "", error = "", signature = "";
        foreach (object[] field in fields.Cast<object[]>())
        {
            var value = field[1];
            switch ((long)field[0])
            {
                case 1: path = (string)value; break;
                case 2: iface = (string)value; break;
                case 3: member = (string)value; break;
                case 4: error = (string)value; break;
                case 5: replySerial = (uint)(long)value; break;
                case 8: signature = (string)value; break;
            }
        }

        reader.Pad(8);
        var body = bodyLength > 0 && signature.Length > 0 ? reader.Values(signature) : [];
        return new DBusMessage(kind, serial, replySerial, path, iface, member, error, body);
    }
}
