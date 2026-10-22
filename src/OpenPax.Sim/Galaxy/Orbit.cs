using OpenPax.Sim.Numerics;

namespace OpenPax.Sim.Galaxy;

/// <summary>
/// Circular motion around a center point, as every orbiting object in the
/// original does (UniverseObject). Angles are in
/// degrees.
/// </summary>
public sealed class Orbit
{
    public Orbit(int centerX, int centerY, int radius, double angle, double speed)
    {
        CenterX = centerX;
        CenterY = centerY;
        Radius = radius;
        Angle = angle;
        Speed = speed;
    }

    public int CenterX { get; }

    public int CenterY { get; }

    public int Radius { get; }

    public double Angle { get; private set; }

    /// <summary>Degrees per step; negative for clockwise.</summary>
    public double Speed { get; }

    public int X { get; private set; }

    public int Y { get; private set; }

    /// <summary>The position as an original save holds it, until the next step moves it.</summary>
    internal void RestorePosition(int x, int y)
    {
        X = x;
        Y = y;
    }

    /// <summary>
    /// Draws a starting angle in tenths of a degree, speed = rate / radius,
    /// reversed 30% of the time.
    /// </summary>
    public static Orbit Draw(GameRandom random, int centerX, int centerY, int radius, double rate)
    {
        var angle = random.Next(3600) * 0.1;
        var speed = rate / radius;
        if (random.Next(100) < 30)
        {
            speed = -speed;
        }

        return new Orbit(centerX, centerY, radius, angle, speed);
    }

    /// <summary>One step: advance the angle, wrap it to [0, 360), place the object.</summary>
    public void Step()
    {
        Angle = Wrap(Angle + Speed);
        (X, Y) = Place(CenterX, CenterY, Radius, Angle);
    }

    /// <summary>Brings an angle into [0, 360).</summary>
    public static double Wrap(double angle)
    {
        while (angle >= 360.0)
        {
            angle -= 360.0;
        }

        while (angle < 0.0)
        {
            angle += 360.0;
        }

        return angle;
    }

    /// <summary>The point at an angle on a circle, rounded as the original rounds it.</summary>
    public static (int X, int Y) Place(int centerX, int centerY, int radius, double angle)
    {
        var rad = X87.DegreesToRadians(angle);
        return (X87.ToInt(centerX + X87.CosTimes(rad, radius)), X87.ToInt(centerY + X87.SinTimes(rad, radius)));
    }
}
