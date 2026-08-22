namespace AstraExtera.Galaxy;

/// <summary>
/// Tight Earth analog for the playable world. Vintage Story geology and gait assume ~1 g and
/// an iron-bearing crust; temperature is held near Earth for now and can be relaxed later.
/// </summary>
public static class EarthAnalog
{
    public const double EarthBulkIronMassFraction = 0.321;
    public const double EarthCoreMassFraction = 0.325;
    public const double EarthSurfaceTemperatureK = 288.0;
    public const double EarthEquilibriumTemperatureK = 255.0;

    public const double MinRadiusEarth = 0.90;
    public const double MaxRadiusEarth = 1.10;
    public const double MinSurfaceGravityG = 0.90;
    public const double MaxSurfaceGravityG = 1.10;
    public const double MinBulkIronMassFraction = 0.28;
    public const double MaxBulkIronMassFraction = 0.36;
    public const double MinCoreMassFraction = 0.28;
    public const double MaxCoreMassFraction = 0.36;
    public const double MinSurfaceTemperatureK = 275.0;
    public const double MaxSurfaceTemperatureK = 300.0;

    public const int MaxAttempts = 64;

    public static EarthAnalogWorld Sample(ref SplitMix64 rng)
    {
        for (var attempt = 0; attempt < MaxAttempts; attempt++)
        {
            var bulkIron = rng.NextGaussian(EarthBulkIronMassFraction, 0.012);
            var coreMassFraction = rng.NextGaussian(EarthCoreMassFraction, 0.012);
            var radiusEarth = rng.NextGaussian(1.0, 0.025);
            var densityEarth = 1.0 + 0.85 * ((bulkIron - EarthBulkIronMassFraction) / EarthBulkIronMassFraction);
            var massEarth = densityEarth * radiusEarth * radiusEarth * radiusEarth;
            var gravityG = massEarth / (radiusEarth * radiusEarth);
            var surfaceTemperatureK = rng.NextGaussian(EarthSurfaceTemperatureK, 4.0);
            var equilibriumTemperatureK = rng.NextGaussian(EarthEquilibriumTemperatureK, 3.0);

            var world = new EarthAnalogWorld(
                radiusEarth,
                massEarth,
                densityEarth,
                gravityG,
                bulkIron,
                coreMassFraction,
                equilibriumTemperatureK,
                surfaceTemperatureK);

            if (IsEarthlike(world))
            {
                return world;
            }
        }

        return Fallback();
    }

    public static bool IsEarthlike(EarthAnalogWorld world)
        => world.RadiusEarth is >= MinRadiusEarth and <= MaxRadiusEarth
           && world.SurfaceGravityG is >= MinSurfaceGravityG and <= MaxSurfaceGravityG
           && world.BulkIronMassFraction is >= MinBulkIronMassFraction and <= MaxBulkIronMassFraction
           && world.CoreMassFraction is >= MinCoreMassFraction and <= MaxCoreMassFraction
           && world.SurfaceTemperatureK is >= MinSurfaceTemperatureK and <= MaxSurfaceTemperatureK
           && world.CoreMassFraction <= world.BulkIronMassFraction + 0.04;

    public static EarthAnalogWorld Fallback()
        => new(
            RadiusEarth: 1.0,
            MassEarth: 1.0,
            MeanDensityEarth: 1.0,
            SurfaceGravityG: 1.0,
            BulkIronMassFraction: EarthBulkIronMassFraction,
            CoreMassFraction: EarthCoreMassFraction,
            EquilibriumTemperatureK: EarthEquilibriumTemperatureK,
            SurfaceTemperatureK: EarthSurfaceTemperatureK);
}

public sealed record EarthAnalogWorld(
    double RadiusEarth,
    double MassEarth,
    double MeanDensityEarth,
    double SurfaceGravityG,
    double BulkIronMassFraction,
    double CoreMassFraction,
    double EquilibriumTemperatureK,
    double SurfaceTemperatureK);
