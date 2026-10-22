namespace OpenPax.Sim.Galaxy;

/// <summary>
/// One of the 18 small orbits every planet is created with:
/// radius 20 to 24, an angle and a speed of 25 / radius. Probably moons or
/// orbital decoration; their use is settled when planets are ported further.
/// </summary>
public sealed record Satellite(int Radius, double Angle, double Speed);
