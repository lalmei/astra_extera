namespace AstraExtera.Galaxy;

public enum GalaxyMorphology
{
    UnbarredSpiral = 0,
    BarredSpiral = 1,
    Elliptical = 2
}

public enum ObserverWorldKind
{
    TerrestrialPlanet = 0,
    TerrestrialMoon = 1
}

public sealed record GalaxyBlueprint(
    GalaxyMorphology Morphology,
    double StellarMassSolar,
    double DiskScaleLengthKpc,
    double ThinDiskScaleHeightPc,
    double BulgeToDiskMass,
    double SolarAnalogMetallicityFeH,
    double MetallicityGradientDexPerKpc,
    double MetallicityScatterDex,
    int SpiralArmCount,
    double SpiralPitchDeg,
    double InnerHabitableRadiusKpc,
    double OuterHabitableRadiusKpc,
    double SersicIndex,
    double AxisRatio,
    double MetallicityReferenceRadiusKpc)
{
    public bool IsElliptical => Morphology == GalaxyMorphology.Elliptical;

    public string MorphologyLabel => Morphology switch
    {
        GalaxyMorphology.BarredSpiral => "barred spiral",
        GalaxyMorphology.UnbarredSpiral => "unbarred spiral",
        GalaxyMorphology.Elliptical => "elliptical",
        _ => Morphology.ToString()
    };
}

public sealed record GalacticLocation(
    double GalactocentricRadiusKpc,
    double AzimuthRad,
    double HeightPc,
    double MetallicityFeH,
    bool InSpiralArm,
    double LocalStellarDensityRelativeToSolar,
    double SupernovaRateRelativeToSolar);

public enum StarSpectralClass
{
    M = 0,
    K = 1,
    G = 2,
    F = 3
}

public enum CompanionRole
{
    InnerRocky = 0,
    ShepherdGiant = 1,
    OuterIceGiant = 2,

    /// <summary>A second gas giant outside the shepherd, the way Saturn trails Jupiter.</summary>
    OuterGasGiant = 3
}

/// <summary>What a ring is made of, which is what sets how bright and how coloured it reads.</summary>
public enum RingComposition
{
    /// <summary>Fresh water ice: the brightest rings, and the ones that lift a giant's magnitude.</summary>
    Ice = 0,

    /// <summary>Rock and dust ground off shepherd moonlets: dim, ruddy, easy to miss.</summary>
    RockAndDust = 1,

    /// <summary>Carbon-dark debris, darker than the planet it circles.</summary>
    Soot = 2
}

/// <summary>
/// A ring system, measured in radii of the planet it circles and lying in that planet's equatorial
/// plane -- so the giant's obliquity is also the ring tilt, and its ascending node is the direction
/// the ring line runs.
/// </summary>
/// <param name="OpticalDepth">How solid the ring reads, from a dust haze at 0 to opaque at 1.</param>
/// <param name="DivisionRadiusPlanetRadii">
/// Where the widest gap sits, swept clear by a resonance with an inner moon. Zero when the ring has
/// no division worth drawing.
/// </param>
public sealed record PlanetRing(
    double InnerRadiusPlanetRadii,
    double OuterRadiusPlanetRadii,
    double OpticalDepth,
    double DivisionRadiusPlanetRadii,
    RingComposition Composition,
    float TintR,
    float TintG,
    float TintB)
{
    public double WidthPlanetRadii => Math.Max(0.0, OuterRadiusPlanetRadii - InnerRadiusPlanetRadii);

    public bool HasDivision => DivisionRadiusPlanetRadii > InnerRadiusPlanetRadii
                               && DivisionRadiusPlanetRadii < OuterRadiusPlanetRadii;

    public string CompositionLabel => Composition switch
    {
        RingComposition.Ice => "water ice",
        RingComposition.RockAndDust => "rock and dust",
        RingComposition.Soot => "sooted debris",
        _ => Composition.ToString()
    };
}

/// <summary>
/// A long-lived storm parked in one of a giant's bands: an anticyclone the size of a small world,
/// held in place between two jets the way Jupiter's Great Red Spot is.
/// </summary>
public sealed record PlanetStorm(
    string Name,
    double LatitudeDeg,
    double LongitudeSpanDeg,
    double LatitudeSpanDeg,
    double AgeYears,
    float TintR,
    float TintG,
    float TintB);

/// <summary>
/// How a giant looks: which way it is tipped, the banding its rotation whips up, the storm caught
/// between two of those bands, and the ring system in its equatorial plane.
/// </summary>
/// <param name="ObliquityDeg">
/// Tilt of the equator -- and so of the rings -- from the orbital plane. A giant with no tilt shows
/// its rings edge-on to anyone in that plane; a tipped one opens them.
/// </param>
/// <param name="Retrograde">The giant spins, and its rings run, against the direction it orbits.</param>
/// <param name="AscendingNodeDeg">
/// Where the equator crosses the orbital plane, which is the direction the ring line runs.
/// </param>
public sealed record GiantAppearance(
    double ObliquityDeg,
    bool Retrograde,
    double RotationPeriodHours,
    double AscendingNodeDeg,
    int BandCount,
    float BandLightR,
    float BandLightG,
    float BandLightB,
    float BandDarkR,
    float BandDarkG,
    float BandDarkB,
    PlanetStorm? Storm,
    PlanetRing? Ring)
{
    public bool HasRing => Ring is not null;

    public bool HasStorm => Storm is not null;
}

/// <summary>
/// A planet other than the playable world. Giants carry the appearance and the moon family that
/// make them worth looking at; rocky companions leave both empty.
/// </summary>
public sealed record CompanionPlanet(
    CompanionRole Role,
    double SemiMajorAxisAu,
    double MassEarth,
    double RadiusEarth,
    double OrbitalPeriodDays,
    GiantAppearance? Appearance = null,
    SystemMoon[]? Moons = null)
{
    /// <summary>Declared rather than positional so an absent family is an empty list, never null.</summary>
    public SystemMoon[] Moons { get; init; } = Moons ?? [];

    public bool IsGiant => Role is CompanionRole.ShepherdGiant
        or CompanionRole.OuterGasGiant
        or CompanionRole.OuterIceGiant;

    public PlanetRing? Ring => Appearance?.Ring;

    public bool Equals(CompanionPlanet? other)
    {
        if (other is null)
        {
            return false;
        }

        if (ReferenceEquals(this, other))
        {
            return true;
        }

        return Role == other.Role
               && SemiMajorAxisAu.Equals(other.SemiMajorAxisAu)
               && MassEarth.Equals(other.MassEarth)
               && RadiusEarth.Equals(other.RadiusEarth)
               && OrbitalPeriodDays.Equals(other.OrbitalPeriodDays)
               && Appearance == other.Appearance
               && Moons.SequenceEqual(other.Moons);
    }

    public override int GetHashCode()
    {
        var hash = new HashCode();
        hash.Add(Role);
        hash.Add(SemiMajorAxisAu);
        hash.Add(MassEarth);
        hash.Add(RadiusEarth);
        hash.Add(OrbitalPeriodDays);
        hash.Add(Appearance);
        foreach (var moon in Moons)
        {
            hash.Add(moon);
        }

        return hash.ToHashCode();
    }
}

/// <summary>
/// One moon of a giant. Distances are in Earth radii from the giant's centre, so a family reads
/// against the same ruler as the Roche limit and the Hill sphere that bracket it.
/// </summary>
public sealed record SystemMoon(
    int Index,
    double OrbitalDistanceEarthRadii,
    double MassEarth,
    double RadiusEarth,
    double DayLengthDays,
    bool Habitable,
    string? Name = null)
{
    /// <summary>The moon's name, or its numeral -- the way an unnamed moon is written.</summary>
    public string DisplayName => Name ?? Numeral(Index);

    private static string Numeral(int index)
    {
        var numerals = new[] { "I", "II", "III", "IV", "V", "VI", "VII", "VIII", "IX", "X" };
        return index >= 1 && index <= numerals.Length
            ? numerals[index - 1]
            : index.ToString(System.Globalization.CultureInfo.InvariantCulture);
    }
}

public sealed record LocalSystemCheck(string Label, bool Passed, string Detail);

public sealed record GalaxyPlacement(
    int SchemaVersion,
    long WorldSeed,
    GalaxyBlueprint Galaxy,
    GalacticLocation Location,
    ObserverWorldKind WorldKind,
    EarthAnalogWorld World,
    LocalSystem System,
    CelestialOrientation Orientation)
{
    public const int CurrentSchemaVersion = 7;

    public bool CanHostIronCore => MetallicityModel.CanHostIronCore(Location.MetallicityFeH);

    public bool CanHostOres => MetallicityModel.CanHostOres(Location.MetallicityFeH);
}
