namespace OpenPax.Data.Installer;

/// <summary>
/// Undoes the change Inno Setup makes to programs and libraries before
/// packing them (docs/formats/inno-setup.md): the address after each x86
/// CALL or JMP was made relative to its place in the file, so repeated
/// calls pack better. This is the form Inno Setup 5.3.9 and later write,
/// which also flips the top byte of backward addresses.
/// </summary>
internal static class CallFilter
{
    private const int Block = 0x10000;

    /// <summary>Restores a whole file in place.</summary>
    public static void Undo(Span<byte> data)
    {
        var i = 0;
        while (i < data.Length)
        {
            var b = data[i++];
            if (b != 0xE8 && b != 0xE9)
            {
                continue;
            }

            // Instructions that cross a 64 KB block were left alone.
            if (Block - ((i - 1) % Block) < 5)
            {
                continue;
            }

            if (i + 4 > data.Length)
            {
                break;
            }

            if (data[i + 3] is 0x00 or 0xFF)
            {
                var place = (uint)(i + 4) & 0xFFFFFF;
                var relative = (data[i] | ((uint)data[i + 1] << 8) | ((uint)data[i + 2] << 16)) - place;
                data[i] = (byte)relative;
                data[i + 1] = (byte)(relative >> 8);
                data[i + 2] = (byte)(relative >> 16);
                if ((relative & 0x800000) != 0)
                {
                    data[i + 3] = (byte)~data[i + 3];
                }
            }

            i += 4;
        }
    }
}
