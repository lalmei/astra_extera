namespace AstraExtera.Galaxy;

/// <summary>
/// Radial iron abundance. Disks are anchored on a solar-neighborhood analog; ellipticals are
/// anchored on the effective radius of a giant, metal-rich spheroid.
/// </summary>
public static class MetallicityModel
{
    public const double IronCoreMinimumFeH = -0.50;
    public const double OreFormingMinimumFeH = -0.30;
    public const double SolarNeighborhoodRadiusKpc = 8.0;
    public const double MaximumSafeSupernovaRate = 2.5;

    public static double MeanFeH(GalaxyBlueprint galaxy, double radiusKpc)
        => galaxy.SolarAnalogMetallicityFeH
           + galaxy.MetallicityGradientDexPerKpc * (radiusKpc - galaxy.MetallicityReferenceRadiusKpc);

    public static double SampleFeH(GalaxyBlueprint galaxy, double radiusKpc, ref SplitMix64 rng)
        => MeanFeH(galaxy, radiusKpc) + rng.NextGaussian(0.0, galaxy.MetallicityScatterDex);

    public static bool CanHostIronCore(double feH) => feH >= IronCoreMinimumFeH;

    public static bool CanHostOres(double feH) => feH >= OreFormingMinimumFeH;

    public static double OuterHabitableRadiusKpc(GalaxyBlueprint galaxy)
    {
        var gradient = galaxy.MetallicityGradientDexPerKpc;
        if (gradient >= 0.0)
        {
            return 15.0;
        }

        var margin = galaxy.MetallicityScatterDex;
        var radius = galaxy.MetallicityReferenceRadiusKpc
                     + (OreFormingMinimumFeH + margin - galaxy.SolarAnalogMetallicityFeH) / gradient;
        var ceiling = galaxy.IsElliptical ? 20.0 : 16.0;
        return Math.Clamp(radius, galaxy.InnerHabitableRadiusKpc + 0.5, ceiling);
    }
}
