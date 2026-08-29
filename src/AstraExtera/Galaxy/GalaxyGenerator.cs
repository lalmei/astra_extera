namespace AstraExtera.Galaxy;

/// <summary>
/// Builds a host galaxy from a world seed, then rejection-samples a habitable site with enough
/// iron for an Earth-like crust. Spirals are the common case. Giant ellipticals are allowed but
/// rare: their cores are dynamically hostile, so the habitable shell lives farther out in a
/// spheroid rather than a thin disk.
/// </summary>
public static class GalaxyGenerator
{
    public const int MaxLocationAttempts = 256;
    public const double EllipticalProbability = 0.025;

    public static GalaxyPlacement Generate(long worldSeed)
    {
        var morphologyRng = new SplitMix64(MixSeed(worldSeed, 0xE11A));
        if (morphologyRng.NextUnit() < EllipticalProbability)
        {
            var ellipticalRng = new SplitMix64(MixSeed(worldSeed, 0xE11B));
            return Place(worldSeed, GenerateElliptical(ref ellipticalRng), ref ellipticalRng);
        }

        var rng = new SplitMix64(MixSeed(worldSeed, 0xA57A));
        return Place(worldSeed, GenerateSpiral(ref rng), ref rng);
    }

    private static GalaxyPlacement Place(long worldSeed, GalaxyBlueprint galaxy, ref SplitMix64 rng)
    {
        var location = SampleHabitableLocation(galaxy, ref rng);
        var worldKind = rng.NextBool(0.28)
            ? ObserverWorldKind.TerrestrialMoon
            : ObserverWorldKind.TerrestrialPlanet;
        var bulk = EarthAnalog.SampleBulk(ref rng);
        var system = LocalSystem.Sample(ref rng, worldKind, bulk, out var world);
        var orientation = CelestialOrientation.Sample(ref rng);

        return new GalaxyPlacement(
            GalaxyPlacement.CurrentSchemaVersion,
            worldSeed,
            galaxy,
            location,
            worldKind,
            world,
            system,
            orientation);
    }

    private static GalaxyBlueprint GenerateSpiral(ref SplitMix64 rng)
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
            12.0,
            SersicIndex: 1.0,
            AxisRatio: thinDiskScaleHeightPc / (diskScaleLengthKpc * 1000.0),
            MetallicityReferenceRadiusKpc: MetallicityModel.SolarNeighborhoodRadiusKpc);

        var outerHabitableRadiusKpc = Math.Max(
            innerHabitableRadiusKpc + 1.5,
            MetallicityModel.OuterHabitableRadiusKpc(draft));

        return draft with { OuterHabitableRadiusKpc = outerHabitableRadiusKpc };
    }

    private static GalaxyBlueprint GenerateElliptical(ref SplitMix64 rng)
    {
        var stellarMassSolar = Math.Exp(rng.NextRange(Math.Log(8.0e10), Math.Log(6.0e11)));
        var effectiveRadiusKpc = rng.NextRange(2.8, 7.5);
        var sersicIndex = rng.NextRange(3.2, 4.8);
        var axisRatio = rng.NextRange(0.55, 0.92);
        var solarAnalogMetallicityFeH = rng.NextRange(0.05, 0.32);
        var metallicityGradientDexPerKpc = rng.NextRange(-0.12, -0.05);
        var metallicityScatterDex = rng.NextRange(0.10, 0.18);
        var innerHabitableRadiusKpc = rng.NextRange(0.45, 0.75) * effectiveRadiusKpc;

        var draft = new GalaxyBlueprint(
            GalaxyMorphology.Elliptical,
            stellarMassSolar,
            DiskScaleLengthKpc: effectiveRadiusKpc,
            ThinDiskScaleHeightPc: axisRatio * effectiveRadiusKpc * 1000.0,
            BulgeToDiskMass: 1.0,
            solarAnalogMetallicityFeH,
            metallicityGradientDexPerKpc,
            metallicityScatterDex,
            SpiralArmCount: 0,
            SpiralPitchDeg: 0.0,
            innerHabitableRadiusKpc,
            OuterHabitableRadiusKpc: 12.0,
            SersicIndex: sersicIndex,
            AxisRatio: axisRatio,
            MetallicityReferenceRadiusKpc: effectiveRadiusKpc);

        var outerHabitableRadiusKpc = Math.Max(
            innerHabitableRadiusKpc + 0.8,
            MetallicityModel.OuterHabitableRadiusKpc(draft));

        return draft with { OuterHabitableRadiusKpc = outerHabitableRadiusKpc };
    }

    private static GalacticLocation SampleHabitableLocation(GalaxyBlueprint galaxy, ref SplitMix64 rng)
        => galaxy.IsElliptical
            ? SampleEllipticalLocation(galaxy, ref rng)
            : SampleDiskLocation(galaxy, ref rng);

    private static GalacticLocation SampleDiskLocation(GalaxyBlueprint galaxy, ref SplitMix64 rng)
    {
        for (var attempt = 0; attempt < MaxLocationAttempts; attempt++)
        {
            var radiusKpc = SampleAreaWeightedRadius(
                galaxy.InnerHabitableRadiusKpc,
                galaxy.OuterHabitableRadiusKpc,
                ref rng);
            var azimuthRad = rng.NextRange(0.0, 2.0 * Math.PI);
            var heightPc = rng.NextGaussian(0.0, galaxy.ThinDiskScaleHeightPc / Math.Sqrt(2.0));
            var location = CreateLocation(galaxy, radiusKpc, azimuthRad, heightPc, ref rng);
            if (IsHabitable(galaxy, location))
            {
                return location;
            }
        }

        return FallbackLocation(galaxy);
    }

    private static GalacticLocation SampleEllipticalLocation(GalaxyBlueprint galaxy, ref SplitMix64 rng)
    {
        for (var attempt = 0; attempt < MaxLocationAttempts; attempt++)
        {
            var structuralRadiusKpc = SampleVolumeWeightedRadius(
                galaxy.InnerHabitableRadiusKpc,
                galaxy.OuterHabitableRadiusKpc,
                ref rng);
            var azimuthRad = rng.NextRange(0.0, 2.0 * Math.PI);
            var mu = rng.NextRange(-1.0, 1.0);
            var cylindricalRadiusKpc = structuralRadiusKpc * Math.Sqrt(Math.Max(0.0, 1.0 - mu * mu));
            var heightPc = structuralRadiusKpc * mu * galaxy.AxisRatio * 1000.0;
            var location = CreateLocation(galaxy, cylindricalRadiusKpc, azimuthRad, heightPc, ref rng);
            if (IsHabitable(galaxy, location))
            {
                return location;
            }
        }

        return FallbackLocation(galaxy);
    }

    private static GalacticLocation CreateLocation(
        GalaxyBlueprint galaxy,
        double radiusKpc,
        double azimuthRad,
        double heightPc,
        ref SplitMix64 rng)
    {
        var structuralRadiusKpc = StructuralRadiusKpc(galaxy, radiusKpc, heightPc);
        var feH = MetallicityModel.SampleFeH(galaxy, structuralRadiusKpc, ref rng);
        var density = StellarDensityRelativeToSolar(galaxy, structuralRadiusKpc, heightPc);
        var supernovaRate = density * (0.65 + 0.35 * (galaxy.InnerHabitableRadiusKpc / Math.Max(0.4, structuralRadiusKpc)));
        return new GalacticLocation(
            radiusKpc,
            azimuthRad,
            heightPc,
            feH,
            InSpiralArm: IsInSpiralArm(galaxy, radiusKpc, azimuthRad),
            LocalStellarDensityRelativeToSolar: density,
            SupernovaRateRelativeToSolar: supernovaRate);
    }

    public static bool IsHabitable(GalaxyBlueprint galaxy, GalacticLocation location)
    {
        var structuralRadiusKpc = StructuralRadiusKpc(galaxy, location);
        if (structuralRadiusKpc < galaxy.InnerHabitableRadiusKpc)
        {
            return false;
        }

        if (structuralRadiusKpc > galaxy.OuterHabitableRadiusKpc)
        {
            return false;
        }

        if (!galaxy.IsElliptical && Math.Abs(location.HeightPc) > 3.0 * galaxy.ThinDiskScaleHeightPc)
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

    public static double StructuralRadiusKpc(GalaxyBlueprint galaxy, GalacticLocation location)
        => StructuralRadiusKpc(galaxy, location.GalactocentricRadiusKpc, location.HeightPc);

    public static double StructuralRadiusKpc(GalaxyBlueprint galaxy, double cylindricalRadiusKpc, double heightPc)
    {
        if (!galaxy.IsElliptical)
        {
            return cylindricalRadiusKpc;
        }

        var zKpc = heightPc / 1000.0;
        var flattenedZ = zKpc / Math.Max(0.2, galaxy.AxisRatio);
        return Math.Sqrt(cylindricalRadiusKpc * cylindricalRadiusKpc + flattenedZ * flattenedZ);
    }

    private static GalacticLocation FallbackLocation(GalaxyBlueprint galaxy)
    {
        var radiusKpc = 0.5 * (galaxy.InnerHabitableRadiusKpc + galaxy.OuterHabitableRadiusKpc);
        return new GalacticLocation(
            radiusKpc,
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

    private static double SampleVolumeWeightedRadius(double innerKpc, double outerKpc, ref SplitMix64 rng)
    {
        var innerCu = innerKpc * innerKpc * innerKpc;
        var outerCu = outerKpc * outerKpc * outerKpc;
        return Math.Cbrt(innerCu + (outerCu - innerCu) * rng.NextUnit());
    }

    /// <summary>
    /// Fraction of the solar-neighborhood stellar density contributed by the diffuse halo. Real
    /// halos are a few tenths of a percent locally; the disk must be allowed to fall below this,
    /// otherwise every sight line out of the plane picks up a bright uniform haze.
    /// </summary>
    private const double HaloDensityRelativeToSolar = 0.002;

    private static double StellarDensityRelativeToSolar(GalaxyBlueprint galaxy, double structuralRadiusKpc, double heightPc)
    {
        if (galaxy.IsElliptical)
        {
            var re = Math.Max(0.5, galaxy.DiskScaleLengthKpc);
            var n = Math.Max(1.0, galaxy.SersicIndex);
            var b = 1.9992 * n - 0.3271;
            var ratio = Math.Max(0.05, structuralRadiusKpc / re);
            var intensity = Math.Exp(-b * (Math.Pow(ratio, 1.0 / n) - 1.0));
            return Math.Clamp(intensity, 1e-4, 40.0);
        }

        var radial = Math.Exp(-(structuralRadiusKpc - MetallicityModel.SolarNeighborhoodRadiusKpc) / galaxy.DiskScaleLengthKpc);
        var vertical = Math.Exp(-Math.Abs(heightPc) / galaxy.ThinDiskScaleHeightPc);
        return radial * vertical;
    }

    /// <summary>Round, faint, r^-3 population that sets the floor far above and below the disk.</summary>
    private static double HaloDensityAt(double sphericalRadiusKpc)
    {
        var scaled = Math.Max(1.0, sphericalRadiusKpc / MetallicityModel.SolarNeighborhoodRadiusKpc);
        return HaloDensityRelativeToSolar / (scaled * scaled * scaled);
    }

    public static double StellarDensityAt(GalaxyBlueprint galaxy, double xKpc, double yKpc, double zKpc)
    {
        var cylindricalRadiusKpc = Math.Sqrt(xKpc * xKpc + yKpc * yKpc);
        var heightPc = zKpc * 1000.0;
        var structuralRadiusKpc = StructuralRadiusKpc(galaxy, cylindricalRadiusKpc, heightPc);
        var density = StellarDensityRelativeToSolar(galaxy, structuralRadiusKpc, heightPc);
        var sphericalRadiusKpc = Math.Sqrt(cylindricalRadiusKpc * cylindricalRadiusKpc + zKpc * zKpc);
        if (galaxy.IsElliptical)
        {
            return density + HaloDensityAt(sphericalRadiusKpc);
        }

        var azimuthRad = Math.Atan2(yKpc, xKpc);
        density *= SpiralArmOverdensity(galaxy, cylindricalRadiusKpc, azimuthRad);
        density += galaxy.BulgeToDiskMass * 6.0 * Math.Exp(-sphericalRadiusKpc / 0.7);
        return density + HaloDensityAt(sphericalRadiusKpc);
    }

    public static double DustDensityAt(GalaxyBlueprint galaxy, double xKpc, double yKpc, double zKpc)
    {
        if (galaxy.IsElliptical)
        {
            return 0.03 * StellarDensityAt(galaxy, xKpc, yKpc, zKpc);
        }

        var cylindricalRadiusKpc = Math.Sqrt(xKpc * xKpc + yKpc * yKpc);
        var dustScaleKpc = 0.12;
        var radial = Math.Exp(-(cylindricalRadiusKpc - MetallicityModel.SolarNeighborhoodRadiusKpc) / galaxy.DiskScaleLengthKpc);
        var azimuthRad = Math.Atan2(yKpc, xKpc);
        // Cold dust tracks the arms more sharply than the stars do, which is what makes dust lanes.
        var armLanes = Math.Pow(SpiralArmOverdensity(galaxy, cylindricalRadiusKpc, azimuthRad), 1.8);
        return 3.4 * Math.Max(0.02, radial) * armLanes * Math.Exp(-Math.Abs(zKpc) / dustScaleKpc);
    }

    public static double SpiralArmAngleRad(GalaxyBlueprint galaxy, int armIndex, double radiusKpc)
    {
        if (galaxy.SpiralArmCount <= 0)
        {
            return 0.0;
        }

        var pitch = galaxy.SpiralPitchDeg * Math.PI / 180.0;
        var logTerm = Math.Log(Math.Max(0.5, radiusKpc) / MetallicityModel.SolarNeighborhoodRadiusKpc);
        var armPhase = logTerm / Math.Tan(Math.Max(0.05, pitch));
        return 2.0 * Math.PI * armIndex / galaxy.SpiralArmCount + armPhase;
    }

    public const double SpiralArmHalfWidthRad = 0.22;
    private const double ArmCrestDensity = 1.7;
    private const double InterarmDensity = 0.72;

    public static bool IsInSpiralArm(GalaxyBlueprint galaxy, double radiusKpc, double azimuthRad)
        => galaxy.SpiralArmCount > 0
           && NearestArmOffsetRad(galaxy, radiusKpc, azimuthRad) < SpiralArmHalfWidthRad;

    /// <summary>
    /// Arms are density waves, not walls: a smooth crest-to-trough profile. A hard edge here shows
    /// up as blocky banding once a sight line is integrated through the disk.
    /// </summary>
    public static double SpiralArmOverdensity(GalaxyBlueprint galaxy, double radiusKpc, double azimuthRad)
    {
        if (galaxy.SpiralArmCount <= 0)
        {
            return 1.0;
        }

        var offset = NearestArmOffsetRad(galaxy, radiusKpc, azimuthRad);
        var sigma = SpiralArmHalfWidthRad / 1.5;
        var crest = Math.Exp(-0.5 * (offset / sigma) * (offset / sigma));
        return InterarmDensity + (ArmCrestDensity - InterarmDensity) * crest;
    }

    private static double NearestArmOffsetRad(GalaxyBlueprint galaxy, double radiusKpc, double azimuthRad)
    {
        var nearest = double.MaxValue;
        for (var arm = 0; arm < galaxy.SpiralArmCount; arm++)
        {
            var delta = Math.Abs(NormalizeAngle(azimuthRad - SpiralArmAngleRad(galaxy, arm, radiusKpc)));
            nearest = Math.Min(nearest, delta);
        }

        return nearest;
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
