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
    OuterIceGiant = 2
}

public sealed record CompanionPlanet(
    CompanionRole Role,
    double SemiMajorAxisAu,
    double MassEarth,
    double RadiusEarth,
    double OrbitalPeriodDays);

public sealed record SystemMoon(
    int Index,
    double OrbitalDistanceEarthRadii,
    double MassEarth,
    double RadiusEarth,
    double DayLengthDays,
    bool Habitable);

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
    public const int CurrentSchemaVersion = 5;

    public bool CanHostIronCore => MetallicityModel.CanHostIronCore(Location.MetallicityFeH);

    public bool CanHostOres => MetallicityModel.CanHostOres(Location.MetallicityFeH);
}
