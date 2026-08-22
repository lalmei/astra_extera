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

public sealed record GalaxyPlacement(
    int SchemaVersion,
    long WorldSeed,
    GalaxyBlueprint Galaxy,
    GalacticLocation Location,
    ObserverWorldKind WorldKind,
    EarthAnalogWorld World,
    CelestialOrientation Orientation)
{
    public const int CurrentSchemaVersion = 4;

    public bool CanHostIronCore => MetallicityModel.CanHostIronCore(Location.MetallicityFeH);

    public bool CanHostOres => MetallicityModel.CanHostOres(Location.MetallicityFeH);
}
