using OpenPax.Host.Update.DBus;

namespace OpenPax.Host.Tests;

/// <summary>Our small D-Bus client's wire format (the Flatpak portal update, docs/updates.md).</summary>
public sealed class DBusTests
{
    [Fact]
    public void AMethodCallReadsBackAsWritten()
    {
        var bytes = DBusConnection.Call(7, "org.freedesktop.portal.Flatpak", "/org/freedesktop/portal/Flatpak/update_monitor/1_21/516", "org.freedesktop.portal.Flatpak.UpdateMonitor", "Update", ["", null]);
        var message = DBusMessage.Read(bytes);
        Assert.Equal(DBusMessage.Kinds.MethodCall, message.Kind);
        Assert.Equal(7u, message.Serial);
        Assert.Equal("/org/freedesktop/portal/Flatpak/update_monitor/1_21/516", message.Path);
        Assert.Equal("org.freedesktop.portal.Flatpak.UpdateMonitor", message.Interface);
        Assert.Equal("Update", message.Member);
        Assert.Equal("", message.Body[0]);
        Assert.Empty((Dictionary<string, object>)message.Body[1]);
    }

    [Fact]
    public void APortalProgressSignalGivesItsFields()
    {
        // A Progress signal as the portal sends it: body a{sv} with uint32 status and progress, and a string message.
        var body = new DBusWriter();
        var lengthAt = Pad4(body);
        body.UInt32(0);
        body.Pad(8);
        var start = body.Length;
        Entry(body, "status", 'u', w => w.UInt32(3));
        Entry(body, "progress", 'u', w => w.UInt32(40));
        Entry(body, "error_message", 's', w => w.String("no space left"));
        body.SetUInt32(lengthAt, (uint)(body.Length - start));
        var message = DBusMessage.Read(Signal("/org/freedesktop/portal/Flatpak/update_monitor/1_9/2", "Progress", "a{sv}", body.ToArray()));

        Assert.Equal(DBusMessage.Kinds.Signal, message.Kind);
        Assert.Equal("Progress", message.Member);
        var info = Assert.IsType<Dictionary<string, object>>(message.Body[0]);
        Assert.Equal(3L, info["status"]);
        Assert.Equal(40L, info["progress"]);
        Assert.Equal("no space left", info["error_message"]);
    }

    private static int Pad4(DBusWriter w)
    {
        w.Pad(4);
        return w.Length;
    }

    private static void Entry(DBusWriter w, string key, char type, Action<DBusWriter> value)
    {
        w.Pad(8);
        w.String(key);
        w.Signature(type.ToString());
        value(w);
    }

    // A whole signal message: the fixed header, path, interface, member and signature fields, then the body.
    private static byte[] Signal(string path, string member, string signature, byte[] body)
    {
        var h = new DBusWriter();
        h.Byte((byte)'l');
        h.Byte((byte)DBusMessage.Kinds.Signal);
        h.Byte(0);
        h.Byte(1);
        h.UInt32((uint)body.Length);
        h.UInt32(99);
        var lengthAt = h.Length;
        h.UInt32(0);
        var start = h.Length;
        h.Pad(8);
        h.Byte(1);
        h.Signature("o");
        h.String(path);
        h.Pad(8);
        h.Byte(2);
        h.Signature("s");
        h.String("org.freedesktop.portal.Flatpak.UpdateMonitor");
        h.Pad(8);
        h.Byte(3);
        h.Signature("s");
        h.String(member);
        h.Pad(8);
        h.Byte(8);
        h.Signature("g");
        h.Signature(signature);
        h.SetUInt32(lengthAt, (uint)(h.Length - start));
        h.Pad(8);
        return [.. h.ToArray(), .. body];
    }
}
