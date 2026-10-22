namespace OpenPax.Media.Video;

/// <summary>
/// Plays a converted movie (MP4, H.264 video, AAC sound) through the
/// shipped FFmpeg libraries, returning RGBA frames and float sound in file
/// order. Not thread safe; use one decoder from one thread.
/// </summary>
public sealed unsafe class MovieDecoder : IDisposable
{
    private readonly Queue<object> _ready = new();
    private nint _format;
    private nint _video;
    private nint _audio;
    private nint _packet;
    private nint _frame;
    private readonly int _videoStream;
    private readonly int _audioStream = -1;
    private readonly double _videoTimeBase;
    private readonly double _audioTimeBase;
    private bool _flushed;

    public MovieDecoder(string path)
    {
        nint format = 0;
        Check(Ffmpeg.avformat_open_input(&format, path, 0, 0));
        _format = format;
        Check(Ffmpeg.avformat_find_stream_info(_format, 0));

        (_videoStream, _video, _videoTimeBase) = OpenStream(FfmpegAbi.MediaVideo, required: true);
        (_audioStream, _audio, _audioTimeBase) = OpenStream(FfmpegAbi.MediaAudio, required: false);
        Width = Ffmpeg.Int32At(Parameters(_videoStream), FfmpegAbi.ParWidth);
        Height = Ffmpeg.Int32At(Parameters(_videoStream), FfmpegAbi.ParHeight);
        _packet = Ffmpeg.av_packet_alloc();
        _frame = Ffmpeg.av_frame_alloc();
    }

    public int Width { get; }

    public int Height { get; }

    public bool HasAudio => _audioStream >= 0;

    /// <summary>The next <see cref="VideoFrame"/> or <see cref="AudioChunk"/>; null at the end.</summary>
    public object? Next()
    {
        while (_ready.Count == 0)
        {
            if (_flushed)
            {
                return null;
            }

            var read = Ffmpeg.av_read_frame(_format, _packet);
            if (read == FfmpegAbi.ErrorEof)
            {
                // Sending no packet drains what the decoders still hold.
                Drain(_video, true, 0);
                if (_audio != 0)
                {
                    Drain(_audio, false, 0);
                }

                _flushed = true;
                continue;
            }

            Check(read);
            var stream = Ffmpeg.Int32At(_packet, FfmpegAbi.PacketStreamIndex);
            if (stream == _videoStream)
            {
                Drain(_video, true, _packet);
            }
            else if (stream == _audioStream)
            {
                Drain(_audio, false, _packet);
            }

            Ffmpeg.av_packet_unref(_packet);
        }

        return _ready.Dequeue();
    }

    public void Dispose()
    {
        var frame = _frame;
        Ffmpeg.av_frame_free(&frame);
        var packet = _packet;
        Ffmpeg.av_packet_free(&packet);
        var video = _video;
        Ffmpeg.avcodec_free_context(&video);
        var audio = _audio;
        if (audio != 0)
        {
            Ffmpeg.avcodec_free_context(&audio);
        }

        var format = _format;
        Ffmpeg.avformat_close_input(&format);
        _frame = _packet = _video = _audio = _format = 0;
    }

    private (int Index, nint Context, double TimeBase) OpenStream(int type, bool required)
    {
        nint codec = 0;
        var index = Ffmpeg.av_find_best_stream(_format, type, -1, -1, &codec, 0);
        if (index < 0)
        {
            return required ? throw new InvalidDataException(Ffmpeg.ErrorText(index)) : (-1, 0, 0);
        }

        var context = Ffmpeg.avcodec_alloc_context3(codec);
        Check(Ffmpeg.avcodec_parameters_to_context(context, Parameters(index)));
        Check(Ffmpeg.avcodec_open2(context, codec, 0));
        var stream = Stream(index);
        var num = Ffmpeg.Int32At(stream, FfmpegAbi.StreamTimeBase);
        var den = Ffmpeg.Int32At(stream, FfmpegAbi.StreamTimeBase + 4);
        return (index, context, den == 0 ? 0 : (double)num / den);
    }

    private nint Stream(int index)
    {
        var streams = Ffmpeg.PointerAt(_format, FfmpegAbi.FormatStreams);
        return Ffmpeg.PointerAt(streams, index * sizeof(nint));
    }

    private nint Parameters(int index)
    {
        return Ffmpeg.PointerAt(Stream(index), FfmpegAbi.StreamCodecpar);
    }

    private void Drain(nint context, bool video, nint packet)
    {
        Check(Ffmpeg.avcodec_send_packet(context, packet));
        while (true)
        {
            var got = Ffmpeg.avcodec_receive_frame(context, _frame);
            if (got == FfmpegErrors.Again || got == FfmpegAbi.ErrorEof)
            {
                return;
            }

            Check(got);
            _ready.Enqueue(video ? ToVideo() : ToAudio());
            Ffmpeg.av_frame_unref(_frame);
        }
    }

    private VideoFrame ToVideo()
    {
        var width = Ffmpeg.Int32At(_frame, FfmpegAbi.FrameWidth);
        var height = Ffmpeg.Int32At(_frame, FfmpegAbi.FrameHeight);
        var format = Ffmpeg.Int32At(_frame, FfmpegAbi.FrameFormat);
        if (format != FfmpegAbi.PixYuv444p && format != FfmpegAbi.PixYuv420p)
        {
            throw new InvalidDataException(string.Format(Strings.UnsupportedPixelFormat, format));
        }

        var rgba = new byte[width * height * 4];
        YuvConverter.ToRgba(_frame, width, height, format == FfmpegAbi.PixYuv444p, rgba);
        return new VideoFrame(Ffmpeg.Int64At(_frame, FfmpegAbi.FramePts) * _videoTimeBase, width, height, rgba);
    }

    private AudioChunk ToAudio()
    {
        var samples = Ffmpeg.Int32At(_frame, FfmpegAbi.FrameNbSamples);
        var channels = Ffmpeg.Int32At(_frame, FfmpegAbi.FrameChannels);
        var format = Ffmpeg.Int32At(_frame, FfmpegAbi.FrameFormat);
        var output = new float[samples * channels];

        // Plane pointers and sizes come from the frame FFmpeg just decoded.
        if (format == FfmpegAbi.SampleFltp)
        {
            for (var c = 0; c < channels; c++)
            {
                var plane = (float*)Ffmpeg.PointerAt(_frame, FfmpegAbi.FrameData + c * sizeof(nint));
                for (var i = 0; i < samples; i++)
                {
                    output[i * channels + c] = plane[i];
                }
            }
        }
        else if (format == FfmpegAbi.SampleFlt)
        {
            new ReadOnlySpan<float>((float*)Ffmpeg.PointerAt(_frame, FfmpegAbi.FrameData), output.Length).CopyTo(output);
        }
        else
        {
            throw new InvalidDataException(string.Format(Strings.UnsupportedSampleFormat, format));
        }

        var rate = Ffmpeg.Int32At(Parameters(_audioStream), FfmpegAbi.ParSampleRate);
        return new AudioChunk(Ffmpeg.Int64At(_frame, FfmpegAbi.FramePts) * _audioTimeBase, rate, channels, output);
    }

    private static void Check(int result)
    {
        if (result < 0)
        {
            throw new InvalidDataException(Ffmpeg.ErrorText(result));
        }
    }
}
