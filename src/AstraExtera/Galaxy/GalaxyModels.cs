namespace AstraExtera.Galaxy;

public enum GalaxyMorphology
{
    UnbarredSpiral = 0,
    BarredSpiral = 1
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
    double OuterHabitableRadiusKpc);

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
    ObserverWorldKind WorldKind)
{
    public const int CurrentSchemaVersion = 1;

    public bool CanHostIronCore => MetallicityModel.CanHostIronCore(Location.MetallicityFeH);

    public bool CanHostOres => MetallicityModel.CanHostOres(Location.MetallicityFeH);
}
