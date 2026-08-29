namespace AstraExtera.Galaxy;

/// <summary>
/// The authored world's viewpoint inside the galaxy, in galactic coordinates: longitude 0 looks
/// at the nucleus, latitude 0 is the galactic midplane. Shared so the integrated band and the
/// sampled stars land on the same sky.
/// </summary>
public readonly struct ObserverFrame
{
    public readonly double OriginXKpc;
    public readonly double OriginYKpc;
    public readonly double OriginZKpc;
    private readonly double towardCenterX;
    private readonly double towardCenterY;
    private readonly double alongPlaneX;
    private readonly double alongPlaneY;

    public ObserverFrame(GalacticLocation location)
    {
        var phi = location.AzimuthRad;
        var radius = location.GalactocentricRadiusKpc;
        OriginXKpc = radius * Math.Cos(phi);
        OriginYKpc = radius * Math.Sin(phi);
        OriginZKpc = location.HeightPc / 1000.0;
        towardCenterX = -Math.Cos(phi);
        towardCenterY = -Math.Sin(phi);
        alongPlaneX = -Math.Sin(phi);
        alongPlaneY = Math.Cos(phi);
    }

    public (double X, double Y, double Z) Direction(double longitudeRad, double latitudeRad)
    {
        var cosB = Math.Cos(latitudeRad);
        var sinB = Math.Sin(latitudeRad);
        var cosL = Math.Cos(longitudeRad);
        var sinL = Math.Sin(longitudeRad);
        return (
            cosB * cosL * towardCenterX + cosB * sinL * alongPlaneX,
            cosB * cosL * towardCenterY + cosB * sinL * alongPlaneY,
            sinB);
    }

    public (double X, double Y, double Z) PointAt((double X, double Y, double Z) direction, double distanceKpc)
        => (
            OriginXKpc + distanceKpc * direction.X,
            OriginYKpc + distanceKpc * direction.Y,
            OriginZKpc + distanceKpc * direction.Z);
}
