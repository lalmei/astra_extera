namespace AstraExtera.Galaxy;

/// <summary>
/// Tight Earth analog for the playable world. Vintage Story geology and gait assume ~1 g and
/// an iron-bearing crust. Size and iron are sampled here; surface temperature is filled in by
/// the local star once the world sits in its habitable zone.
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

    /// <summary>Size, gravity and iron only. Temperature is Earth's until climate is applied.</summary>
    public static EarthAnalogWorld SampleBulk(ref SplitMix64 rng)
    {
        for (var attempt = 0; attempt < MaxAttempts; attempt++)
        {
            var bulkIron = rng.NextGaussian(EarthBulkIronMassFraction, 0.012);
            var coreMassFraction = rng.NextGaussian(EarthCoreMassFraction, 0.012);
            var radiusEarth = rng.NextGaussian(1.0, 0.025);
            var densityEarth = 1.0 + 0.85 * ((bulkIron - EarthBulkIronMassFraction) / EarthBulkIronMassFraction);
            var massEarth = densityEarth * radiusEarth * radiusEarth * radiusEarth;
            var gravityG = massEarth / (radiusEarth * radiusEarth);

            var world = new EarthAnalogWorld(
                radiusEarth,
                massEarth,
                densityEarth,
                gravityG,
                bulkIron,
                coreMassFraction,
                EarthEquilibriumTemperatureK,
                EarthSurfaceTemperatureK);

            if (IsEarthlikeBulk(world))
            {
                return world;
            }
        }

        return Fallback();
    }

    public static EarthAnalogWorld Sample(ref SplitMix64 rng)
    {
        var bulk = SampleBulk(ref rng);
        for (var attempt = 0; attempt < MaxAttempts; attempt++)
        {
            var world = WithClimate(
                bulk,
                rng.NextGaussian(EarthEquilibriumTemperatureK, 3.0),
                rng.NextGaussian(EarthSurfaceTemperatureK, 4.0));
            if (IsEarthlike(world))
            {
                return world;
            }
        }

        return WithClimate(bulk, EarthEquilibriumTemperatureK, EarthSurfaceTemperatureK);
    }

    public static EarthAnalogWorld WithClimate(
        EarthAnalogWorld bulk,
        double equilibriumTemperatureK,
        double surfaceTemperatureK)
        => bulk with
        {
            EquilibriumTemperatureK = equilibriumTemperatureK,
            SurfaceTemperatureK = surfaceTemperatureK
        };

    public static bool IsEarthlikeBulk(EarthAnalogWorld world)
        => world.RadiusEarth is >= MinRadiusEarth and <= MaxRadiusEarth
           && world.SurfaceGravityG is >= MinSurfaceGravityG and <= MaxSurfaceGravityG
           && world.BulkIronMassFraction is >= MinBulkIronMassFraction and <= MaxBulkIronMassFraction
           && world.CoreMassFraction is >= MinCoreMassFraction and <= MaxCoreMassFraction
           && world.CoreMassFraction <= world.BulkIronMassFraction + 0.04;

    public static bool IsEarthlike(EarthAnalogWorld world)
        => IsEarthlikeBulk(world)
           && world.SurfaceTemperatureK is >= MinSurfaceTemperatureK and <= MaxSurfaceTemperatureK;

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
