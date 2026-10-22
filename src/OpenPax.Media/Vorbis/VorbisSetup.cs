namespace OpenPax.Media.Vorbis;

/// <summary>The three Vorbis header packets (spec section 4.2), parsed.</summary>
internal sealed class VorbisSetup
{
    private VorbisSetup(int channels, int sampleRate, int block0, int block1, Codebook[] books, Floor1[] floors, Residue[] residues, Mapping[] mappings, Mode[] modes)
    {
        Channels = channels;
        SampleRate = sampleRate;
        ShortBlock = block0;
        LongBlock = block1;
        Codebooks = books;
        Floors = floors;
        Residues = residues;
        Mappings = mappings;
        Modes = modes;
    }

    public int Channels { get; }

    public int SampleRate { get; }

    public int ShortBlock { get; }

    public int LongBlock { get; }

    public Codebook[] Codebooks { get; }

    public Floor1[] Floors { get; }

    public Residue[] Residues { get; }

    public Mapping[] Mappings { get; }

    public Mode[] Modes { get; }

    public static VorbisSetup Read(byte[] identification, byte[] comments, byte[] setup)
    {
        var id = new BitReader(identification);
        ExpectHeader(id, 1);
        if (id.Read(32) != 0)
        {
            throw new InvalidDataException(Strings.NotVorbis);
        }

        var channels = (int)id.Read(8);
        var rate = (int)id.Read(32);
        id.Read(32); // bitrate maximum
        id.Read(32); // bitrate nominal
        id.Read(32); // bitrate minimum
        var block0 = 1 << (int)id.Read(4);
        var block1 = 1 << (int)id.Read(4);
        if (channels == 0 || rate == 0 || block0 < 64 || block1 > 8192 || block0 > block1 || !id.Flag())
        {
            throw new InvalidDataException(Strings.NotVorbis);
        }

        // The comment header is only checked; tags are not needed.
        ExpectHeader(new BitReader(comments), 3);

        var r = new BitReader(setup);
        ExpectHeader(r, 5);
        var books = new Codebook[r.Read(8) + 1];
        for (var i = 0; i < books.Length; i++)
        {
            books[i] = Codebook.Read(r);
        }

        var times = r.Read(6) + 1;
        for (var i = 0; i < times; i++)
        {
            if (r.Read(16) != 0)
            {
                throw new InvalidDataException(Strings.BadVorbisSetup);
            }
        }

        var floors = new Floor1[r.Read(6) + 1];
        for (var i = 0; i < floors.Length; i++)
        {
            var type = r.Read(16);
            floors[i] = type switch
            {
                1 => Floor1.Read(r, books.Length),
                0 => throw new InvalidDataException(Strings.Floor0),
                _ => throw new InvalidDataException(Strings.BadVorbisSetup),
            };
        }

        var residues = new Residue[r.Read(6) + 1];
        for (var i = 0; i < residues.Length; i++)
        {
            var type = (int)r.Read(16);
            residues[i] = type <= 2 ? Residue.Read(r, type, books) : throw new InvalidDataException(Strings.BadVorbisSetup);
        }

        var mappings = new Mapping[r.Read(6) + 1];
        for (var i = 0; i < mappings.Length; i++)
        {
            mappings[i] = Mapping.Read(r, channels, floors.Length, residues.Length);
        }

        var modes = new Mode[r.Read(6) + 1];
        for (var i = 0; i < modes.Length; i++)
        {
            var longBlock = r.Flag();
            var window = r.Read(16);
            var transform = r.Read(16);
            var mapping = (int)r.Read(8);
            if (window != 0 || transform != 0 || mapping >= mappings.Length)
            {
                throw new InvalidDataException(Strings.BadVorbisSetup);
            }

            modes[i] = new Mode(longBlock, mapping);
        }

        if (!r.Flag() || r.EndOfPacket)
        {
            throw new InvalidDataException(Strings.BadVorbisSetup);
        }

        return new VorbisSetup(channels, rate, block0, block1, books, floors, residues, mappings, modes);
    }

    private static void ExpectHeader(BitReader r, int type)
    {
        if (r.Read(8) != type)
        {
            throw new InvalidDataException(Strings.NotVorbis);
        }

        foreach (var c in "vorbis")
        {
            if (r.Read(8) != c)
            {
                throw new InvalidDataException(Strings.NotVorbis);
            }
        }
    }
}
