namespace AstraExtera.Galaxy;

/// <summary>
/// How the world's spin axis sits relative to its galaxy, which is what turns a galactic star
/// position into the right ascension and declination a sky renderer needs.
/// <para>
/// There is no reason for a planet's pole to line up with its galaxy, so the pole is drawn
/// uniformly over the sphere. The consequence is visible from the ground: the angle between the
/// celestial pole and the galactic plane decides whether this world's band of light wheels
/// overhead each night or sits nearly fixed near the horizon.
/// </para>
/// </summary>
public sealed record CelestialOrientation(
    double PoleGalacticLongitudeRad,
    double PoleGalacticLatitudeRad,
    double RightAscensionOriginRollRad)
{
    public static CelestialOrientation Sample(ref SplitMix64 rng)
    {
        var latitude = Math.Asin(1.0 - 2.0 * rng.NextUnit());
        var longitude = rng.NextRange(-Math.PI, Math.PI);
        var roll = rng.NextRange(0.0, 2.0 * Math.PI);
        return new CelestialOrientation(longitude, latitude, roll);
    }

    /// <summary>Angle between the celestial pole and the galactic pole; Earth's is about 63°.</summary>
    public double PoleTiltFromGalacticPoleDeg
        => (Math.PI / 2.0 - Math.Abs(PoleGalacticLatitudeRad)) * 180.0 / Math.PI;

    /// <summary>
    /// Converts a galactic direction, longitude 0 at the nucleus, into equatorial coordinates in
    /// degrees with right ascension in [0, 360).
    /// </summary>
    public (double RightAscensionDeg, double DeclinationDeg) ToEquatorial(
        double galacticLongitudeRad,
        double galacticLatitudeRad)
    {
        var cosB = Math.Cos(galacticLatitudeRad);
        var star = (
            X: cosB * Math.Cos(galacticLongitudeRad),
            Y: cosB * Math.Sin(galacticLongitudeRad),
            Z: Math.Sin(galacticLatitudeRad));

        var (pole, raOrigin, third) = Basis();
        var declination = Math.Asin(Math.Clamp(Dot(star, pole), -1.0, 1.0));
        var rightAscension = Math.Atan2(Dot(star, third), Dot(star, raOrigin));
        if (rightAscension < 0.0)
        {
            rightAscension += 2.0 * Math.PI;
        }

        return (rightAscension * 180.0 / Math.PI, declination * 180.0 / Math.PI);
    }

    private ((double X, double Y, double Z) Pole,
             (double X, double Y, double Z) RaOrigin,
             (double X, double Y, double Z) Third) Basis()
    {
        var cosB = Math.Cos(PoleGalacticLatitudeRad);
        var pole = (
            X: cosB * Math.Cos(PoleGalacticLongitudeRad),
            Y: cosB * Math.Sin(PoleGalacticLongitudeRad),
            Z: Math.Sin(PoleGalacticLatitudeRad));

        // Any vector not parallel to the pole works as a seed for the equatorial plane; the roll
        // then fixes where right ascension zero falls.
        var seed = Math.Abs(pole.Z) < 0.9 ? (0.0, 0.0, 1.0) : (1.0, 0.0, 0.0);
        var reference = Normalize(Subtract(seed, Scale(pole, Dot(seed, pole))));
        var perpendicular = Cross(pole, reference);

        var cosRoll = Math.Cos(RightAscensionOriginRollRad);
        var sinRoll = Math.Sin(RightAscensionOriginRollRad);
        var raOrigin = Normalize(Add(Scale(reference, cosRoll), Scale(perpendicular, sinRoll)));
        return (pole, raOrigin, Cross(pole, raOrigin));
    }

    private static double Dot((double X, double Y, double Z) a, (double X, double Y, double Z) b)
        => a.X * b.X + a.Y * b.Y + a.Z * b.Z;

    private static (double X, double Y, double Z) Cross(
        (double X, double Y, double Z) a,
        (double X, double Y, double Z) b)
        => (a.Y * b.Z - a.Z * b.Y, a.Z * b.X - a.X * b.Z, a.X * b.Y - a.Y * b.X);

    private static (double X, double Y, double Z) Add(
        (double X, double Y, double Z) a,
        (double X, double Y, double Z) b)
        => (a.X + b.X, a.Y + b.Y, a.Z + b.Z);

    private static (double X, double Y, double Z) Subtract(
        (double X, double Y, double Z) a,
        (double X, double Y, double Z) b)
        => (a.X - b.X, a.Y - b.Y, a.Z - b.Z);

    private static (double X, double Y, double Z) Scale((double X, double Y, double Z) a, double factor)
        => (a.X * factor, a.Y * factor, a.Z * factor);

    private static (double X, double Y, double Z) Normalize((double X, double Y, double Z) a)
    {
        var length = Math.Sqrt(Dot(a, a));
        return length < 1e-12 ? (1.0, 0.0, 0.0) : Scale(a, 1.0 / length);
    }
}
