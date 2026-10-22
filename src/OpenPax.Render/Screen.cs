using OpenPax.Data.Art;
using OpenPax.Render.TrueColor;

namespace OpenPax.Render;

/// <summary>
/// Shows the game in an SDL window at the window's own resolution
/// (decisions.md, "A modern UI toolkit, drawn in true color"). The
/// original screens' 640x480 palette picture is scaled by the largest whole
/// number that fits, with nearest pixels, centered with black bars; the
/// true-color canvas of the new controls is laid over the whole window.
/// </summary>
public sealed class Screen : IDisposable
{
    private readonly nint _renderer;
    private int _width;
    private int _height;
    private nint _texture;
    private byte[] _rgba = [];
    private int _textureWidth;
    private int _textureHeight;
    private nint _canvasTexture;
    private int _canvasWidth;
    private int _canvasHeight;
    private nint _movieTexture;
    private int _movieWidth;
    private int _movieHeight;

    private Screen(nint renderer, int width, int height)
    {
        _renderer = renderer;
        _width = width;
        _height = height;
    }

    /// <param name="width">The original screens' size in their own coordinates (640x480).</param>
    public static Screen? Create(nint window, int width, int height)
    {
        var renderer = Sdl.Sdl.CreateRenderer(window);
        if (renderer == 0)
        {
            return null;
        }

        Sdl.Sdl.SetRenderLogicalPresentation(renderer, 0, 0, Sdl.Sdl.LogicalPresentationDisabled);
        return new Screen(renderer, width, height);
    }

    /// <summary>The window's size in pixels: the canvas is made this size.</summary>
    public (int Width, int Height) OutputSize => Sdl.Sdl.RenderOutputSize(_renderer);

    /// <summary>Whether presenting waits for the display's refresh; false when the renderer cannot.</summary>
    public bool SetVSync(bool on) => Sdl.Sdl.SetRenderVSync(_renderer, on ? 1 : 0);

    /// <summary>The original screens' width in their own pixels: 640, or more for a screen laid out wide (docs/ui/widescreen.md).</summary>
    public int ClassicWidth
    {
        get => _width;
        set => _width = Math.Max(1, value);
    }

    /// <summary>Their height: 480, or more.</summary>
    public int ClassicHeight
    {
        get => _height;
        set => _height = Math.Max(1, value);
    }

    /// <summary>Where the original screens' picture goes, and the forms' scale.</summary>
    public ClassicPlacement Classic => ClassicPlacement.For(OutputSize.Width, OutputSize.Height, _width, _height);

    /// <summary>
    /// The original screens' picture fitted to the window, sharp when it is
    /// a whole multiple of its own size and smoothed when not, with the
    /// canvas over it.
    /// </summary>
    public void Present(Framebuffer frame, Palette palette, Canvas? overlay = null)
    {
        if (!EnsureTexture(frame.PixelWidth, frame.PixelHeight))
        {
            return;
        }

        frame.ToRgba(palette, _rgba);
        Sdl.Sdl.UpdateTexture(_texture, _rgba, _textureWidth * 4);
        Sdl.Sdl.SetRenderDrawColor(_renderer, 0, 0, 0, 255);
        Sdl.Sdl.RenderClear(_renderer);
        var c = Classic;
        Sdl.Sdl.SetTextureScaleMode(_texture, c.Width % _textureWidth == 0 ? Sdl.Sdl.ScaleModeNearest : Sdl.Sdl.ScaleModeLinear);
        Sdl.Sdl.RenderTexture(_renderer, _texture, c.X, c.Y, c.Width, c.Height);
        if (overlay is not null && UploadCanvas(overlay))
        {
            Sdl.Sdl.RenderTexture(_renderer, _canvasTexture, 0, 0, overlay.Width, overlay.Height);
        }

        Sdl.Sdl.RenderPresent(_renderer);
    }

    /// <summary>Shows a full color picture (a movie frame) at the size of the original screens' picture.</summary>
    public void PresentRgba(byte[] rgba, int width, int height)
    {
        if (_movieTexture == 0 || _movieWidth != width || _movieHeight != height)
        {
            if (_movieTexture != 0)
            {
                Sdl.Sdl.DestroyTexture(_movieTexture);
            }

            _movieTexture = Sdl.Sdl.CreateTexture(_renderer, Sdl.Sdl.PixelFormatRgba32, Sdl.Sdl.TextureAccessStreaming, width, height);
            _movieWidth = width;
            _movieHeight = height;
            if (_movieTexture == 0)
            {
                return;
            }
        }

        Sdl.Sdl.UpdateTexture(_movieTexture, rgba, width * 4);
        Sdl.Sdl.SetRenderDrawColor(_renderer, 0, 0, 0, 255);
        Sdl.Sdl.RenderClear(_renderer);
        var c = Classic;
        Sdl.Sdl.SetTextureScaleMode(_movieTexture, Sdl.Sdl.ScaleModeLinear);
        Sdl.Sdl.RenderTexture(_renderer, _movieTexture, c.X, c.Y, c.Width, c.Height);
        Sdl.Sdl.RenderPresent(_renderer);
    }

    /// <summary>A window mouse position in the original screens' pixels (may fall outside them in the bars).</summary>
    public (int X, int Y) ToScreen(float windowX, float windowY)
    {
        var (x, y) = ToCanvas(windowX, windowY);
        return Classic.ToClassic(x, y, _width, _height);
    }

    /// <summary>A window mouse position in canvas pixels.</summary>
    public (int X, int Y) ToCanvas(float windowX, float windowY)
    {
        var (x, y) = Sdl.Sdl.RenderCoordinatesFromWindow(_renderer, windowX, windowY);
        return ((int)MathF.Floor(x), (int)MathF.Floor(y));
    }

    public void Dispose()
    {
        if (_movieTexture != 0)
        {
            Sdl.Sdl.DestroyTexture(_movieTexture);
        }

        if (_canvasTexture != 0)
        {
            Sdl.Sdl.DestroyTexture(_canvasTexture);
        }

        if (_texture != 0)
        {
            Sdl.Sdl.DestroyTexture(_texture);
        }

        Sdl.Sdl.DestroyRenderer(_renderer);
    }

    /// <summary>The original screens' texture at the framebuffer's pixel size, made again if that changes.</summary>
    private bool EnsureTexture(int width, int height)
    {
        if (_texture != 0 && _textureWidth == width && _textureHeight == height)
        {
            return true;
        }

        if (_texture != 0)
        {
            Sdl.Sdl.DestroyTexture(_texture);
        }

        _texture = Sdl.Sdl.CreateTexture(_renderer, Sdl.Sdl.PixelFormatRgba32, Sdl.Sdl.TextureAccessStreaming, width, height);
        (_textureWidth, _textureHeight) = (width, height);
        _rgba = new byte[width * height * 4];
        return _texture != 0;
    }

    private bool UploadCanvas(Canvas canvas)
    {
        if (_canvasTexture == 0 || _canvasWidth != canvas.Width || _canvasHeight != canvas.Height)
        {
            if (_canvasTexture != 0)
            {
                Sdl.Sdl.DestroyTexture(_canvasTexture);
            }

            _canvasTexture = Sdl.Sdl.CreateTexture(_renderer, Sdl.Sdl.PixelFormatRgba32, Sdl.Sdl.TextureAccessStreaming, canvas.Width, canvas.Height);
            (_canvasWidth, _canvasHeight) = (canvas.Width, canvas.Height);
            if (_canvasTexture == 0)
            {
                return false;
            }

            Sdl.Sdl.SetTextureBlendMode(_canvasTexture, Sdl.Sdl.BlendModeBlend);
            Sdl.Sdl.SetTextureScaleMode(_canvasTexture, Sdl.Sdl.ScaleModeNearest);
        }

        Sdl.Sdl.UpdateTexture(_canvasTexture, System.Runtime.InteropServices.MemoryMarshal.AsBytes(canvas.Pixels.AsSpan()), canvas.Width * 4);
        return true;
    }
}
