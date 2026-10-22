namespace OpenPax.Media.Vorbis;

/// <summary>Mapping type 0 (spec 4.2.4): channel coupling and which floor and residue each channel uses.</summary>
internal sealed record Mapping(int[] Magnitude, int[] Angle, int[] Mux, int[] SubmapFloor, int[] SubmapResidue)
{
    public static Mapping Read(BitReader r, int channels, int floors, int residues)
    {
        if (r.Read(16) != 0)
        {
            throw new InvalidDataException(Strings.BadVorbisSetup);
        }

        var submaps = r.Flag() ? (int)r.Read(4) + 1 : 1;
        int[] magnitude = [];
        int[] angle = [];
        if (r.Flag())
        {
            var steps = (int)r.Read(8) + 1;
            magnitude = new int[steps];
            angle = new int[steps];
            var bits = VorbisMath.ILog(channels - 1);
            for (var i = 0; i < steps; i++)
            {
                magnitude[i] = (int)r.Read(bits);
                angle[i] = (int)r.Read(bits);
                if (magnitude[i] == angle[i] || magnitude[i] >= channels || angle[i] >= channels)
                {
                    throw new InvalidDataException(Strings.BadVorbisSetup);
                }
            }
        }

        if (r.Read(2) != 0)
        {
            throw new InvalidDataException(Strings.BadVorbisSetup);
        }

        var mux = new int[channels];
        if (submaps > 1)
        {
            for (var c = 0; c < channels; c++)
            {
                mux[c] = (int)r.Read(4);
                if (mux[c] >= submaps)
                {
                    throw new InvalidDataException(Strings.BadVorbisSetup);
                }
            }
        }

        var floor = new int[submaps];
        var residue = new int[submaps];
        for (var s = 0; s < submaps; s++)
        {
            r.Read(8); // unused time configuration
            floor[s] = (int)r.Read(8);
            residue[s] = (int)r.Read(8);
            if (floor[s] >= floors || residue[s] >= residues)
            {
                throw new InvalidDataException(Strings.BadVorbisSetup);
            }
        }

        return new Mapping(magnitude, angle, mux, floor, residue);
    }
}
