using System.Runtime.InteropServices;

namespace OpenPax.Media.Video;

/// <summary>
/// Our bindings to the decode-only FFmpeg libraries the game ships
/// (docs/decisions.md, video codec). Only the calls the movie decoder
/// needs. Struct fields are read through <see cref="FfmpegAbi"/> offsets,
/// generated from the pinned headers.
/// </summary>
internal static unsafe partial class Ffmpeg
{
    // Resolved by the host's platform folder to the copies next to the
    // executable, loaded in dependency order.
    private const string Avutil = FfmpegLibraries.Avutil;
    private const string Avcodec = FfmpegLibraries.Avcodec;
    private const string Avformat = FfmpegLibraries.Avformat;

    [LibraryImport(Avformat, StringMarshalling = StringMarshalling.Utf8)]
    public static partial int avformat_open_input(nint* context, string url, nint format, nint options);

    [LibraryImport(Avformat)]
    public static partial int avformat_find_stream_info(nint context, nint options);

    [LibraryImport(Avformat)]
    public static partial int av_find_best_stream(nint context, int type, int wanted, int related, nint* decoder, int flags);

    [LibraryImport(Avformat)]
    public static partial int av_read_frame(nint context, nint packet);

    [LibraryImport(Avformat)]
    public static partial void avformat_close_input(nint* context);

    [LibraryImport(Avcodec)]
    public static partial nint avcodec_alloc_context3(nint codec);

    [LibraryImport(Avcodec)]
    public static partial int avcodec_parameters_to_context(nint context, nint parameters);

    [LibraryImport(Avcodec)]
    public static partial int avcodec_open2(nint context, nint codec, nint options);

    [LibraryImport(Avcodec)]
    public static partial int avcodec_send_packet(nint context, nint packet);

    [LibraryImport(Avcodec)]
    public static partial int avcodec_receive_frame(nint context, nint frame);

    [LibraryImport(Avcodec)]
    public static partial void avcodec_free_context(nint* context);

    [LibraryImport(Avcodec)]
    public static partial nint av_packet_alloc();

    [LibraryImport(Avcodec)]
    public static partial void av_packet_unref(nint packet);

    [LibraryImport(Avcodec)]
    public static partial void av_packet_free(nint* packet);

    [LibraryImport(Avutil)]
    public static partial nint av_frame_alloc();

    [LibraryImport(Avutil)]
    public static partial void av_frame_unref(nint frame);

    [LibraryImport(Avutil)]
    public static partial void av_frame_free(nint* frame);

    [LibraryImport(Avutil)]
    private static partial int av_strerror(int error, byte* buffer, nuint size);

    /// <summary>FFmpeg's message for an error code.</summary>
    public static string ErrorText(int error)
    {
        // The buffer is ours and FFmpeg writes at most its size, with a terminator.
        var buffer = stackalloc byte[256];
        return av_strerror(error, buffer, 256) == 0 ? Marshal.PtrToStringUTF8((nint)buffer) ?? error.ToString() : error.ToString();
    }

    /// <summary>Reads an int32 field at a generated offset.</summary>
    public static int Int32At(nint p, int offset)
    {
        // Callers pass a live FFmpeg object and an offset from FfmpegAbi,
        // which was taken from the headers of these exact libraries.
        return *(int*)(p + offset);
    }

    public static long Int64At(nint p, int offset)
    {
        // As Int32At.
        return *(long*)(p + offset);
    }

    public static nint PointerAt(nint p, int offset)
    {
        // As Int32At.
        return *(nint*)(p + offset);
    }
}
