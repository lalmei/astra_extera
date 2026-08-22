namespace AstraExtera.Galaxy;

/// <summary>
/// Radial iron abundance for a thin-disk analog of the Milky Way.
/// Rocky planets with iron cores need enough Type Ia supernova products; Vintage Story ores
/// need a still higher floor so siderophile veins are plausible.
/// </summary>
public static class MetallicityModel
{
    public const double IronCoreMinimumFeH = -0.50;
    public const double OreFormingMinimumFeH = -0.30;
    public const double SolarNeighborhoodRadiusKpc = 8.0;
    public const double MaximumSafeSupernovaRate = 2.5;

    public static double MeanFeH(GalaxyBlueprint galaxy, double radiusKpc)
        => galaxy.SolarAnalogMetallicityFeH
           + galaxy.MetallicityGradientDexPerKpc * (radiusKpc - SolarNeighborhoodRadiusKpc);

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
        var radius = SolarNeighborhoodRadiusKpc
                     + (OreFormingMinimumFeH + margin - galaxy.SolarAnalogMetallicityFeH) / gradient;
        return Math.Clamp(radius, galaxy.InnerHabitableRadiusKpc + 0.5, 16.0);
    }
}
