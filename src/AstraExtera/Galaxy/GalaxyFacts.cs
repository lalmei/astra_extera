using System.Globalization;

namespace AstraExtera.Galaxy;

public sealed record GalaxyFact(string Term, string Value);

public sealed record GalaxyFactSection(string Heading, IReadOnlyList<GalaxyFact> Rows);

/// <summary>
/// The written description of a placement, shared by the static preview page and the in-game panel.
/// <para>
/// Kept in one place because the two readers are checked against each other by eye: a number that
/// means one thing on the debug page and another in the game panel is worse than no number at all.
/// Units stay inside Latin-1, since the in-game font is not guaranteed to carry the astronomical
/// symbols -- a solar mass renders as "Msun" rather than risking a missing-glyph box.
/// </para>
/// </summary>
public static class GalaxyFacts
{
    public static IReadOnlyList<GalaxyFactSection> Describe(GalaxyPlacement placement, StarField starField)
    {
        ArgumentNullException.ThrowIfNull(placement);
        ArgumentNullException.ThrowIfNull(starField);

        return
        [
            new GalaxyFactSection("Galaxy", GalaxyRows(placement)),
            new GalaxyFactSection("Observer", ObserverRows(placement)),
            new GalaxyFactSection("Earth analog", WorldRows(placement)),
            new GalaxyFactSection("Visible sky", SkyRows(placement, starField))
        ];
    }

    public static string Title(GalaxyPlacement placement)
    {
        ArgumentNullException.ThrowIfNull(placement);
        return $"AstraExtera galaxy preview - seed {placement.WorldSeed}";
    }

    /// <summary>Shorter than <see cref="Title"/>, for the in-game title bar.</summary>
    public static string PanelTitle(GalaxyPlacement placement)
    {
        ArgumentNullException.ThrowIfNull(placement);
        return $"Host galaxy - seed {placement.WorldSeed}";
    }

    public static string Lede(GalaxyPlacement placement)
    {
        ArgumentNullException.ThrowIfNull(placement);
        return placement.Galaxy.IsElliptical
            ? "Giant elliptical: no disk or arms. The gold ring is a spherical habitable shell outside the dense core; the red mark is this save's observer."
            : "Face-on disk and edge-on height for the server-authored galactic site. The gold ring is the habitable annulus; the red mark is this save's observer.";
    }

    private static List<GalaxyFact> GalaxyRows(GalaxyPlacement placement)
    {
        var galaxy = placement.Galaxy;
        var rows = new List<GalaxyFact>();

        if (galaxy.IsElliptical)
        {
            rows.Add(new GalaxyFact("Morphology", $"{galaxy.MorphologyLabel}, Sersic n {F(galaxy.SersicIndex)}, q {F(galaxy.AxisRatio)}"));
            rows.Add(new GalaxyFact("Stellar mass", $"{galaxy.StellarMassSolar:0.00e+0} Msun"));
            rows.Add(new GalaxyFact("Spheroid", $"Re {F(galaxy.DiskScaleLengthKpc)} kpc"));
        }
        else
        {
            rows.Add(new GalaxyFact("Morphology", $"{galaxy.MorphologyLabel}, {galaxy.SpiralArmCount} arms, pitch {F(galaxy.SpiralPitchDeg)}°"));
            rows.Add(new GalaxyFact("Stellar mass", $"{galaxy.StellarMassSolar:0.00e+0} Msun"));
            rows.Add(new GalaxyFact("Disk", $"Rd {F(galaxy.DiskScaleLengthKpc)} kpc, thin-disk h {F(galaxy.ThinDiskScaleHeightPc)} pc, B/D {F(galaxy.BulgeToDiskMass)}"));
        }

        rows.Add(new GalaxyFact("Habitable zone", $"{F(galaxy.InnerHabitableRadiusKpc)} - {F(galaxy.OuterHabitableRadiusKpc)} kpc"));
        rows.Add(new GalaxyFact(
            "Metallicity",
            $"gradient {F(galaxy.MetallicityGradientDexPerKpc)} dex/kpc, [Fe/H] at {F(galaxy.MetallicityReferenceRadiusKpc)} kpc = {F(galaxy.SolarAnalogMetallicityFeH)}, scatter {F(galaxy.MetallicityScatterDex)} dex"));
        return rows;
    }

    private static List<GalaxyFact> ObserverRows(GalaxyPlacement placement)
    {
        var location = placement.Location;
        return
        [
            new GalaxyFact("World", placement.WorldKind == ObserverWorldKind.TerrestrialMoon ? "terrestrial moon" : "terrestrial planet"),
            new GalaxyFact(
                "Location",
                $"R {F(location.GalactocentricRadiusKpc)} kpc, az {F(location.AzimuthRad * 180.0 / Math.PI)}°, z {F(location.HeightPc)} pc"),
            new GalaxyFact("[Fe/H]", location.MetallicityFeH.ToString("+0.00;-0.00", CultureInfo.InvariantCulture)),
            new GalaxyFact("Iron / ores", $"{YesNo(placement.CanHostIronCore)} / {YesNo(placement.CanHostOres)}"),
            new GalaxyFact(
                "Spiral arm",
                placement.Galaxy.IsElliptical ? "none" : location.InSpiralArm ? "inside an arm" : "interarm"),
            new GalaxyFact(
                "Local density",
                $"rho/rho_sun {F(location.LocalStellarDensityRelativeToSolar)}, SN/SN_sun {F(location.SupernovaRateRelativeToSolar)}")
        ];
    }

    private static List<GalaxyFact> WorldRows(GalaxyPlacement placement)
    {
        var world = placement.World;
        return
        [
            new GalaxyFact("Radius", $"{F(world.RadiusEarth)} Rearth"),
            new GalaxyFact("Mass", $"{F(world.MassEarth)} Mearth"),
            new GalaxyFact("Surface gravity", $"{F(world.SurfaceGravityG)} g"),
            new GalaxyFact("Bulk iron", $"{F(world.BulkIronMassFraction * 100.0)} wt% (Earth 32.1)"),
            new GalaxyFact("Core mass", $"{F(world.CoreMassFraction * 100.0)} % (Earth 32.5)"),
            new GalaxyFact("Mean density", $"{F(world.MeanDensityEarth)} rho_earth"),
            new GalaxyFact("Surface temperature", $"{F(world.SurfaceTemperatureK)} K (placeholder climate; may change)"),
            new GalaxyFact("Equilibrium temperature", $"{F(world.EquilibriumTemperatureK)} K")
        ];
    }

    private static List<GalaxyFact> SkyRows(GalaxyPlacement placement, StarField starField)
    {
        var rows = new List<GalaxyFact>
        {
            new("Limiting magnitude", $"{F(starField.LimitingMagnitude)} (dark-adapted naked eye)"),
            new(
                "Effective limit",
                $"{F(starField.EffectiveLimitingMagnitude)}{(starField.Truncated ? " (render budget reached first)" : string.Empty)}"),
            new("Naked-eye stars", $"{starField.ExpectedVisibleCount:N0} expected, {starField.SampledCount:N0} drawn"),
            new(
                "Resolved / rendered",
                $"{starField.Stars.Count:N0}{(starField.Truncated ? " (budget capped)" : string.Empty)}"),
            new(
                "Celestial pole",
                $"{F(placement.Orientation.PoleTiltFromGalacticPoleDeg)}° from the galactic pole (Earth 62.9°)")
        };

        if (starField.Stars.Count > 0)
        {
            var brightest = starField.Stars[0];
            rows.Add(new GalaxyFact(
                "Brightest star",
                $"m {F(brightest.ApparentMagnitude)}, M {F(brightest.AbsoluteMagnitude)}, {F(brightest.DistancePc)} pc, A_V {F(brightest.ExtinctionMagnitudes)}"));
            rows.Add(new GalaxyFact("Median distance", $"{F(MedianDistancePc(starField))} pc"));
        }

        return rows;
    }

    private static double MedianDistancePc(StarField starField)
    {
        var distances = starField.Stars.Select(static star => star.DistancePc).OrderBy(static d => d).ToArray();
        return distances[distances.Length / 2];
    }

    private static string YesNo(bool value) => value ? "yes" : "no";

    private static string F(double value)
        => value.ToString("0.###", CultureInfo.InvariantCulture);
}
