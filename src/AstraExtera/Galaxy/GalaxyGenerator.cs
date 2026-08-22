namespace AstraExtera.Galaxy;

/// <summary>
/// Builds a Milky Way analog from a world seed, then rejection-samples a thin-disk location
/// inside that galaxy's habitable annulus. Only iron- and ore-capable sites are kept, because
/// later sky and geology both assume an Earth-like crust.
/// </summary>
public static class GalaxyGenerator
{
    public const int MaxLocationAttempts = 256;

    public static GalaxyPlacement Generate(long worldSeed)
    {
        var rng = new SplitMix64(MixSeed(worldSeed, 0xA57A));
        var galaxy = GenerateGalaxy(ref rng);
        var location = SampleHabitableLocation(galaxy, ref rng);
        var worldKind = rng.NextBool(0.28)
            ? ObserverWorldKind.TerrestrialMoon
            : ObserverWorldKind.TerrestrialPlanet;

        return new GalaxyPlacement(
            GalaxyPlacement.CurrentSchemaVersion,
            worldSeed,
            galaxy,
            location,
            worldKind);
    }

    private static GalaxyBlueprint GenerateGalaxy(ref SplitMix64 rng)
    {
        var barred = rng.NextBool(0.65);
        var morphology = barred ? GalaxyMorphology.BarredSpiral : GalaxyMorphology.UnbarredSpiral;
        var stellarMassSolar = Math.Exp(rng.NextRange(Math.Log(3.0e10), Math.Log(1.2e11)));
        var diskScaleLengthKpc = rng.NextRange(2.2, 4.2);
        var thinDiskScaleHeightPc = rng.NextRange(220.0, 380.0);
        var bulgeToDiskMass = barred ? rng.NextRange(0.25, 0.45) : rng.NextRange(0.12, 0.30);
        var solarAnalogMetallicityFeH = rng.NextRange(-0.08, 0.10);
        var metallicityGradientDexPerKpc = rng.NextRange(-0.075, -0.045);
        var metallicityScatterDex = rng.NextRange(0.08, 0.14);
        var armCount = rng.NextBool(0.7) ? 4 : 2;
        var spiralPitchDeg = rng.NextRange(10.0, 18.0);
        var innerHabitableRadiusKpc = barred ? rng.NextRange(5.5, 7.0) : rng.NextRange(4.5, 6.5);

        var draft = new GalaxyBlueprint(
            morphology,
            stellarMassSolar,
            diskScaleLengthKpc,
            thinDiskScaleHeightPc,
            bulgeToDiskMass,
            solarAnalogMetallicityFeH,
            metallicityGradientDexPerKpc,
            metallicityScatterDex,
            armCount,
            spiralPitchDeg,
            innerHabitableRadiusKpc,
            12.0);

        var outerHabitableRadiusKpc = Math.Max(
            innerHabitableRadiusKpc + 1.5,
            MetallicityModel.OuterHabitableRadiusKpc(draft));

        return draft with { OuterHabitableRadiusKpc = outerHabitableRadiusKpc };
    }

    private static GalacticLocation SampleHabitableLocation(GalaxyBlueprint galaxy, ref SplitMix64 rng)
    {
        for (var attempt = 0; attempt < MaxLocationAttempts; attempt++)
        {
            var radiusKpc = SampleAreaWeightedRadius(
                galaxy.InnerHabitableRadiusKpc,
                galaxy.OuterHabitableRadiusKpc,
                ref rng);
            var azimuthRad = rng.NextRange(0.0, 2.0 * Math.PI);
            var heightPc = rng.NextGaussian(0.0, galaxy.ThinDiskScaleHeightPc / Math.Sqrt(2.0));
            var feH = MetallicityModel.SampleFeH(galaxy, radiusKpc, ref rng);
            var density = StellarDensityRelativeToSolar(galaxy, radiusKpc, heightPc);
            var supernovaRate = density * (0.65 + 0.35 * (galaxy.InnerHabitableRadiusKpc / radiusKpc));
            var inArm = IsInSpiralArm(galaxy, radiusKpc, azimuthRad);

            var location = new GalacticLocation(
                radiusKpc,
                azimuthRad,
                heightPc,
                feH,
                inArm,
                density,
                supernovaRate);

            if (IsHabitable(galaxy, location))
            {
                return location;
            }
        }

        return FallbackSolarAnalog(galaxy);
    }

    public static bool IsHabitable(GalaxyBlueprint galaxy, GalacticLocation location)
    {
        if (location.GalactocentricRadiusKpc < galaxy.InnerHabitableRadiusKpc)
        {
            return false;
        }

        if (location.GalactocentricRadiusKpc > galaxy.OuterHabitableRadiusKpc)
        {
            return false;
        }

        if (Math.Abs(location.HeightPc) > 3.0 * galaxy.ThinDiskScaleHeightPc)
        {
            return false;
        }

        if (location.SupernovaRateRelativeToSolar > MetallicityModel.MaximumSafeSupernovaRate)
        {
            return false;
        }

        return MetallicityModel.CanHostIronCore(location.MetallicityFeH)
               && MetallicityModel.CanHostOres(location.MetallicityFeH);
    }

    private static GalacticLocation FallbackSolarAnalog(GalaxyBlueprint galaxy)
    {
        return new GalacticLocation(
            MetallicityModel.SolarNeighborhoodRadiusKpc,
            0.0,
            20.0,
            Math.Max(MetallicityModel.OreFormingMinimumFeH, galaxy.SolarAnalogMetallicityFeH),
            InSpiralArm: false,
            LocalStellarDensityRelativeToSolar: 1.0,
            SupernovaRateRelativeToSolar: 1.0);
    }

    private static double SampleAreaWeightedRadius(double innerKpc, double outerKpc, ref SplitMix64 rng)
    {
        var innerSq = innerKpc * innerKpc;
        var outerSq = outerKpc * outerKpc;
        return Math.Sqrt(innerSq + (outerSq - innerSq) * rng.NextUnit());
    }

    private static double StellarDensityRelativeToSolar(GalaxyBlueprint galaxy, double radiusKpc, double heightPc)
    {
        var radial = Math.Exp(-(radiusKpc - MetallicityModel.SolarNeighborhoodRadiusKpc) / galaxy.DiskScaleLengthKpc);
        var vertical = Math.Exp(-Math.Abs(heightPc) / galaxy.ThinDiskScaleHeightPc);
        return Math.Max(0.05, radial * vertical);
    }

    private static bool IsInSpiralArm(GalaxyBlueprint galaxy, double radiusKpc, double azimuthRad)
    {
        var pitch = galaxy.SpiralPitchDeg * Math.PI / 180.0;
        var logTerm = Math.Log(Math.Max(0.5, radiusKpc) / MetallicityModel.SolarNeighborhoodRadiusKpc);
        var armPhase = logTerm / Math.Tan(pitch);
        var twoPi = 2.0 * Math.PI;
        var nearest = double.MaxValue;
        for (var arm = 0; arm < galaxy.SpiralArmCount; arm++)
        {
            var armAngle = twoPi * arm / galaxy.SpiralArmCount + armPhase;
            var delta = Math.Abs(NormalizeAngle(azimuthRad - armAngle));
            nearest = Math.Min(nearest, delta);
        }

        return nearest < 0.22;
    }

    private static double NormalizeAngle(double radians)
    {
        var wrapped = Math.IEEERemainder(radians, 2.0 * Math.PI);
        return Math.Abs(wrapped);
    }

    private static long MixSeed(long worldSeed, long salt)
    {
        unchecked
        {
            var mixed = (ulong)worldSeed ^ ((ulong)salt * 0x9E3779B97F4A7C15UL);
            mixed ^= mixed >> 30;
            mixed *= 0xBF58476D1CE4E5B9UL;
            mixed ^= mixed >> 27;
            mixed *= 0x94D049BB133111EBUL;
            mixed ^= mixed >> 31;
            return (long)mixed;
        }
    }
}
