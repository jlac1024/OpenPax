using System.Buffers.Binary;
using OpenPax.Data.Resources;

namespace OpenPax.Data.Views;

/// <summary>
/// Reads <c>VIEW</c> resources (Data/ViewData): a window (class 8000) and
/// its widgets, each stored as an int32 class id and its fields, groups and
/// windows holding counted children. Layout in docs/formats/views.md.
/// </summary>
public static class ViewReader
{
    public const int WindowClass = 8000;
    public const int GroupClass = 8200;

    // Classes that store only the shared base; all their drawing is in code.
    private static readonly HashSet<int> _plainPanels = [8038, 8040, 9002, 10001, 13002, 14001, 14900, 18002, 19500, 19650, 19800, 22002, 24001, 24002, 26001, 26002];
    private static readonly HashSet<int> _byteOptions = [9000, 10002, 10003, 18001];

    public static ViewNode Parse(ReadOnlySpan<byte> body)
    {
        if (body.Length < 4 || BinaryPrimitives.ReadInt32LittleEndian(body) != WindowClass)
        {
            throw new ResourceFormatException(Strings.BadView);
        }

        var r = new RecordReader(body, Strings.BadView);
        var view = ReadObject(ref r);
        if (!r.AtEnd)
        {
            throw new ResourceFormatException(Strings.BadView);
        }

        return view;
    }

    // (class id, then the class's own Read)
    private static ViewNode ReadObject(ref RecordReader r)
    {
        var classId = r.Int32();
        var b = ReadBase(ref r);
        switch (classId)
        {
            case WindowClass:
                {
                    // container fields, a byte the original
                    // reads and throws away, then the title.
                    var children = ReadChildren(ref r);
                    r.Byte();
                    return new ViewNode(classId, b, null, children, r.String(), null);
                }

            case GroupClass:
                {
                    var background = r.Int32();
                    return new ViewNode(classId, b, null, ReadChildren(ref r), null, background);
                }

            default:
                return new ViewNode(classId, b, ReadDetails(classId, ref r), [], null, null);
        }
    }

    private static WidgetBase ReadBase(ref RecordReader r)
    {
        var id = r.Int32();
        var left = r.Int32();
        var top = r.Int32();
        var width = r.Int32();
        var height = r.Int32();
        var visible = r.Int32();
        var enabled = r.Int32();
        // The original turns a stored 1 ("shown") into 2 ("wants to be
        // shown") on load; 1 is set at runtime once the parent is shown.
        return new WidgetBase(id, left, top, width, height, visible == 1 ? 2 : visible, enabled);
    }

    private static List<ViewNode> ReadChildren(ref RecordReader r)
    {
        var count = r.Int16();
        if (count < 0)
        {
            throw new ResourceFormatException(Strings.BadView);
        }

        var children = new List<ViewNode>(count);
        for (var i = 0; i < count; i++)
        {
            children.Add(ReadObject(ref r));
        }

        return children;
    }

    private static WidgetDetails? ReadDetails(int classId, ref RecordReader r)
    {
        if (_plainPanels.Contains(classId))
        {
            return null;
        }

        if (_byteOptions.Contains(classId))
        {
            return new ByteOptionDetails(r.Byte());
        }

        return classId switch
        {
            8002 => ReadButton(ref r, animated: false),
            8039 => ReadButton(ref r, animated: true),
            8009 => new PictureDetails(r.Int32()),
            8016 => new CheckBoxDetails(new TextRef(r.Int32(), r.Int32()), r.Byte(), r.Byte(), r.Byte(), r.Byte(), r.Byte(), r.Int32(), r.Int32()),
            8017 => new TextEditDetails(r.Int32(), r.Byte(), r.Byte(), r.Byte(), r.Byte(), r.Byte(), r.Byte()),
            8019 => new ListBoxDetails(r.Int32(), r.Int32(), r.Byte(), r.Byte()),
            8021 => new HotSpotDetails(r.Int32(), r.Int32()),
            8024 => new TextFieldDetails(new TextStyle(r.Byte(), r.Byte(), r.Byte(), r.Byte(), r.Byte())),
            8025 => new LabelDetails(new TextRef(r.Int32(), r.Int32()), new TextStyle(r.Int32(), r.Int32(), r.Int32(), r.Int32(), r.Int32())),
            8027 => new SpinnerDetails(ReadRange(ref r), r.Int32(), r.Int32(), r.Int32(), r.Int32(), r.Byte()),
            8028 => new TextFieldDetails(new TextStyle(r.Int32(), r.Int32(), r.Int32(), r.Int32(), r.Int32())),
            8033 => new ScrollBarDetails(r.Int32(), r.Int32(), r.Int32(), r.Int32(), r.Int32(), r.Byte(), r.Int32()),
            8034 => ReadHeaderBar(ref r),
            8035 => new StateBoxDetails(new TextRef(r.Int32(), r.Int32()), r.Byte(), r.Byte(), r.Byte(), r.Byte(), r.Int32(), r.Int32()),
            8037 => new AnimationDetails(r.Int32(), r.Int32(), r.Int32(), r.Byte(), r.Int32(), r.Byte(), r.Byte()),
            8041 => new VolumeSliderDetails(ReadRange(ref r), r.Int32()),
            8042 => new EmpireToggleDetails(r.Int32(), r.Int32()),

            // The original stops at an unknown class with the view half read;
            // there is no size to skip it by, so the view is refused.
            _ => throw new ResourceFormatException(string.Format(Strings.UnknownViewClass, classId)),
        };
    }

    private static ButtonDetails ReadButton(ref RecordReader r, bool animated)
    {
        var latching = r.Byte();
        var picture = r.Int32();
        var text = new TextRef(r.Int32(), r.Int32());
        var color = r.Byte();
        var pressed = r.Byte();
        var disabled = r.Byte();
        var shadow = r.Byte();
        var align = r.Byte();
        var style = r.Byte();
        var font = r.Byte();
        var textX = r.Int32();
        var textY = r.Int32();
        var pressX = r.Int32();
        var pressY = r.Int32();
        var sound = r.Int32();
        int? first = animated ? r.Int32() : null;
        int? frames = animated ? r.Int32() : null;
        return new ButtonDetails(latching, picture, text, color, pressed, disabled, shadow, align, style, font, textX, textY, pressX, pressY, sound, first, frames);
    }

    private static ValueRange ReadRange(ref RecordReader r)
    {
        return new ValueRange(r.Int32(), r.Int32(), r.Int32(), r.Int32());
    }

    private static HeaderBarDetails ReadHeaderBar(ref RecordReader r)
    {
        var target = r.Int32();
        var count = r.Int32();
        if (count < 0)
        {
            throw new ResourceFormatException(Strings.BadView);
        }

        var cells = new List<HeaderCell>(count);
        for (var i = 0; i < count; i++)
        {
            var list = r.Int16();
            var index = r.Byte();
            cells.Add(new HeaderCell(new TextRef(list, index), r.Byte(), r.Int32(), r.Int32(), r.Int32(), r.Int32(), r.Byte(), r.Int32(), r.Byte(), r.Byte(), r.Byte(), r.Byte()));
        }

        return new HeaderBarDetails(target, cells);
    }
}
