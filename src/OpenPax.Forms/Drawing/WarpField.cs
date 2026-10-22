using OpenPax.Render.TrueColor;

namespace OpenPax.Forms.Drawing;

/// <summary>
/// A starfield flown through slowly, as at warp: stars come out of the
/// distance at the center, grow brighter and draw short streaks as they
/// pass. Drawn behind forms in place of the still backdrop. Presentation
/// only, so its own small random generator and the frame clock are fine.
/// </summary>
public sealed class WarpField
{
    /// <summary>Stars per million canvas pixels.</summary>
    private const int Density = 1400;

    /// <summary>The longest streak, in pixels at scale 1.</summary>
    private const float MaxTrail = 5;

    /// <summary>The longest step simulated at once, so a stalled frame does not jump the field.</summary>
    private const int MaxStepMs = 100;

    // Mostly white with a cool cast, some of the pink the game's own
    // starfield art has, and now and then a blue or a red star (one in
    // twenty each).
    private static readonly (byte R, byte G, byte B)[] _tints =
    [
        .. Enumerable.Repeat<(byte, byte, byte)>((235, 238, 255), 12),
        .. Enumerable.Repeat<(byte, byte, byte)>((210, 220, 255), 3),
        .. Enumerable.Repeat<(byte, byte, byte)>((255, 200, 205), 3),
        (110, 150, 255),
        (255, 80, 70),
    ];

    private Star[] _stars = [];
    private int _width;
    private int _height;
    private long _last = -1;
    private uint _seed = 0x9E3779B9;

    /// <summary>Depth crossed per second at the default speed; 1 is the whole way from the far plane to the eye.</summary>
    public const float DefaultSpeed = 0.0125f;

    /// <summary>Depth crossed per second; 0 holds the stars still.</summary>
    public float Speed { get; set; } = DefaultSpeed;

    /// <summary>Moves the field on to a time and draws it into a canvas rectangle (canvas pixels), pixel size by scale.</summary>
    public void Draw(Canvas canvas, int x, int y, int width, int height, long now, int scale)
    {
        if (width <= 0 || height <= 0)
        {
            return;
        }

        if (width != _width || height != _height)
        {
            Reset(width, height);
        }

        var step = _last < 0 ? 0 : Math.Clamp(now - _last, 0, MaxStepMs) / 1000f;
        _last = now;
        var (cx, cy, focal) = (x + (width / 2f), y + (height / 2f), Math.Max(width, height) / 2f);
        var size = Math.Max(1, scale);
        for (var i = 0; i < _stars.Length; i++)
        {
            ref var s = ref _stars[i];
            s.Z -= Speed * step;
            var (sx, sy) = (cx + (s.X / s.Z * focal), cy + (s.Y / s.Z * focal));
            if (s.Z <= 0.02f || sx < x - 4 || sx >= x + width + 4 || sy < y - 4 || sy >= y + height + 4)
            {
                s = Spawn(far: true);
                continue;
            }

            // Brighter and larger as it nears, with a short streak pointing
            // back at the center, never longer than a few pixels: a slow warp.
            var light = 0.35f + (0.65f * Math.Clamp(1 - s.Z, 0f, 1f));
            var (dx, dy) = (sx - cx, sy - cy);
            var reach = MathF.Sqrt((dx * dx) + (dy * dy));
            var length = Math.Min(MaxTrail * size, reach * Speed * 0.5f / s.Z);
            var (tx, ty) = reach < 1 ? (sx, sy) : (sx - (dx / reach * length), sy - (dy / reach * length));
            var dot = s.Z < 0.2f ? size * 2 : size;
            Streak(canvas, tx, ty, sx, sy, dot, s.Tint, light);
        }
    }

    private void Reset(int width, int height)
    {
        (_width, _height) = (width, height);
        _stars = new Star[Math.Max(50, (int)((long)width * height * Density / 1_000_000))];
        for (var i = 0; i < _stars.Length; i++)
        {
            _stars[i] = Spawn(far: false);
        }
    }

    /// <summary>A new star anywhere on the far plane's view (or at any depth, when filling the field the first time).</summary>
    private Star Spawn(bool far)
    {
        var z = far ? 1f : 0.05f + (0.95f * Next());
        var aspect = _height / (float)Math.Max(_width, _height);
        var across = _width / (float)Math.Max(_width, _height);
        return new Star((Next() * 2 - 1) * across * z, (Next() * 2 - 1) * aspect * z, z, _tints[(int)(Next() * _tints.Length) % _tints.Length]);
    }

    private float Next()
    {
        // xorshift32
        _seed ^= _seed << 13;
        _seed ^= _seed >> 17;
        _seed ^= _seed << 5;
        return (_seed & 0xFFFFFF) / (float)0x1000000;
    }

    private static void Streak(Canvas canvas, float x0, float y0, float x1, float y1, int dot, (byte R, byte G, byte B) tint, float light)
    {
        var steps = (int)Math.Max(Math.Abs(x1 - x0), Math.Abs(y1 - y0));
        for (var k = 0; k <= steps; k++)
        {
            var t = steps == 0 ? 1f : k / (float)steps;
            var a = (byte)(255 * light * (0.25f + (0.75f * t)));
            canvas.FillRect((int)(x0 + ((x1 - x0) * t)), (int)(y0 + ((y1 - y0) * t)), k == steps ? dot : Math.Max(1, dot / 2), k == steps ? dot : Math.Max(1, dot / 2), Color.Rgb(tint.R, tint.G, tint.B, a));
        }
    }

    private record struct Star(float X, float Y, float Z, (byte R, byte G, byte B) Tint);
}
