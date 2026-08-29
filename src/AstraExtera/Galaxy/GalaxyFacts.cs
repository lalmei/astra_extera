using System.Globalization;

namespace AstraExtera.Galaxy;

public sealed record GalaxyFact(string Term, string Value);

public sealed record GalaxyFactSection(string Heading, IReadOnlyList<GalaxyFact> Rows);

/// <summary>
/// The written description of a placement, shared by the static preview page and the in-game panel.
/// <para>
/// Kept in one place because the two readers are checked against each other by eye: a number that
/// means one thing on the debug page and another in the game panel is worse than no number at all.
/// </para>
/// <para>
/// Solar and Earth units carry their real symbols, <see cref="Sun"/> and <see cref="Earth"/>. None
/// of the three fonts Vintage Story ships has a glyph for either, so the in-game panel draws them
/// as vectors instead of as text -- see the symbol handling in the panel painter. The static
/// preview page renders them as ordinary characters, because a browser font does carry them.
/// </para>
/// </summary>
public static class GalaxyFacts
{
    /// <summary>Sun symbol (U+2609), for solar masses, radii and luminosities.</summary>
    public const string Sun = "\u2609";

    /// <summary>Earth symbol (U+2295), for Earth masses, radii and densities.</summary>
    public const string Earth = "\u2295";

    public static IReadOnlyList<GalaxyFactSection> Describe(
        GalaxyPlacement placement,
        StarField starField,
        LocalSystemSky? localSky = null)
    {
        ArgumentNullException.ThrowIfNull(placement);
        ArgumentNullException.ThrowIfNull(starField);

        var sections = new List<GalaxyFactSection>
        {
            new("Galaxy", GalaxyRows(placement)),
            new("Observer", ObserverRows(placement)),
            new("Local system", SystemRows(placement)),
            new("Earth analog", WorldRows(placement)),
            new("Visible sky", SkyRows(placement, starField))
        };

        if (localSky is not null)
        {
            sections.Add(new GalaxyFactSection("Wanderers", WandererRows(localSky)));
        }

        return sections;
    }

    public static IReadOnlyList<GalaxyFactSection> Describe(GalaxySky sky)
    {
        ArgumentNullException.ThrowIfNull(sky);
        return Describe(sky.Placement, sky.StarField, sky.LocalSky);
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
            rows.Add(new GalaxyFact("Stellar mass", $"{galaxy.StellarMassSolar:0.00e+0} M\u2609"));
            rows.Add(new GalaxyFact("Spheroid", $"Re {F(galaxy.DiskScaleLengthKpc)} kpc"));
        }
        else
        {
            rows.Add(new GalaxyFact("Morphology", $"{galaxy.MorphologyLabel}, {galaxy.SpiralArmCount} arms, pitch {F(galaxy.SpiralPitchDeg)}°"));
            rows.Add(new GalaxyFact("Stellar mass", $"{galaxy.StellarMassSolar:0.00e+0} M\u2609"));
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
                $"rho/rho\u2609 {F(location.LocalStellarDensityRelativeToSolar)}, SN/SN\u2609 {F(location.SupernovaRateRelativeToSolar)}")
        ];
    }

    private static List<GalaxyFact> SystemRows(GalaxyPlacement placement)
    {
        var system = placement.System;
        var rows = new List<GalaxyFact>
        {
            new("Star", $"{system.StarClassLabel}, {F(system.StarMassSolar)} M\u2609, {F(system.StarRadiusSolar)} R\u2609, {F(system.LuminositySolar)} L\u2609"),
            new("Lifespan", $"{F(system.StarLifespanGyr)} Gyr"),
            new("Liquid-water belt", $"{F(system.HabitableZoneInnerAu)} - {F(system.HabitableZoneOuterAu)} AU"),
            new(
                placement.WorldKind == ObserverWorldKind.TerrestrialMoon ? "Parent orbit" : "Orbit",
                $"{F(system.OrbitalDistanceAu)} AU, year {F(system.OrbitalPeriodDays)} d"),
            new("Climate", $"albedo {F(system.BondAlbedo)}, greenhouse {F(system.GreenhouseDeltaK)} K"),
            new("Snow line", $"{F(system.SnowLineAu)} AU")
        };

        if (placement.WorldKind == ObserverWorldKind.TerrestrialMoon)
        {
            rows.Add(new GalaxyFact("Parent giant", $"{F(system.ParentGiantMassEarth ?? 0.0)} M\u2295"));
            rows.Add(new GalaxyFact(
                "Moon orbit",
                $"{F(system.MoonOrbitalDistanceEarthRadii ?? 0.0)} R\u2295, tidal day {F(system.MoonDayLengthDays ?? 0.0)} d, Roche {F(system.RocheLimitEarthRadii ?? 0.0)} R\u2295"));
            if (system.Moons.Length > 1)
            {
                rows.Add(new GalaxyFact("Moon family", $"{system.Moons.Length} moons, habitable is #{system.HabitableMoonIndex}"));
            }
        }

        if (placement.WorldKind == ObserverWorldKind.TerrestrialMoon && system.ParentGiantAppearance is { } parent)
        {
            rows.Add(new GalaxyFact("Parent giant face", DescribeFace(parent)));
            if (parent.Ring is { } parentRing)
            {
                rows.Add(new GalaxyFact("Parent giant rings", DescribeRing(parentRing, parent)));
            }
        }

        // Terms are the panel's own row labels, so two ice giants cannot share one. A role that
        // appears more than once is numbered from the star outward.
        var roleCounts = system.Companions
            .GroupBy(static body => body.Role)
            .ToDictionary(static group => group.Key, static group => group.Count());
        var seen = new Dictionary<CompanionRole, int>();

        foreach (var body in system.Companions)
        {
            seen[body.Role] = seen.GetValueOrDefault(body.Role) + 1;
            var term = roleCounts[body.Role] > 1
                ? $"{CompanionTerm(body.Role)} {seen[body.Role]}"
                : CompanionTerm(body.Role);

            rows.Add(new GalaxyFact(
                term,
                $"{F(body.SemiMajorAxisAu)} AU, {F(body.MassEarth)} M\u2295, {F(body.RadiusEarth)} R\u2295, year {F(body.OrbitalPeriodDays)} d"));

            if (body.Appearance is not { } appearance)
            {
                continue;
            }

            rows.Add(new GalaxyFact($"{term} face", DescribeFace(appearance)));

            if (appearance.Ring is { } ring)
            {
                rows.Add(new GalaxyFact($"{term} rings", DescribeRing(ring, appearance)));
            }

            if (appearance.Storm is { } storm)
            {
                rows.Add(new GalaxyFact(
                    $"{term} storm",
                    $"{storm.Name}, a {F(storm.LongitudeSpanDeg)} degree anticyclone at {F(Math.Abs(storm.LatitudeDeg))} degrees " +
                    $"{(storm.LatitudeDeg >= 0.0 ? "north" : "south")}, standing {storm.AgeYears:N0} years"));
            }

            if (body.Moons.Length > 0)
            {
                var largest = body.Moons.MaxBy(static moon => moon.RadiusEarth)!;
                rows.Add(new GalaxyFact(
                    $"{term} moons",
                    $"{body.Moons.Length} moon{(body.Moons.Length == 1 ? string.Empty : "s")}, " +
                    $"largest {largest.DisplayName} at {F(largest.OrbitalDistanceEarthRadii)} R\u2295, " +
                    $"{F(largest.RadiusEarth)} R\u2295 across, month {F(largest.DayLengthDays)} d"));
            }
        }

        return rows;
    }

    private static string DescribeFace(GiantAppearance appearance)
        => $"{appearance.BandCount} bands, {F(appearance.RotationPeriodHours)} h day, " +
           $"tipped {F(appearance.ObliquityDeg)} degrees{(appearance.Retrograde ? ", spinning backwards" : string.Empty)}";

    private static string DescribeRing(PlanetRing ring, GiantAppearance appearance)
    {
        var openness = GiantAppearances.RingOpenness(appearance);
        var seen = openness < 0.05 ? "seen edge-on" : openness < 0.35 ? "barely open" : "wide open";
        return $"{ring.CompositionLabel} rings, {F(ring.InnerRadiusPlanetRadii)}-{F(ring.OuterRadiusPlanetRadii)} planet radii, " +
               $"running {F(appearance.AscendingNodeDeg)} degrees from the orbital plane's node, {seen} " +
               $"({F(-GiantAppearances.RingBrightnessBoostMagnitudes(appearance))} mag brighter)" +
               $"{(ring.HasDivision ? $", divided at {F(ring.DivisionRadiusPlanetRadii)}" : string.Empty)}";
    }

    private static string CompanionTerm(CompanionRole role)
        => role switch
        {
            CompanionRole.InnerRocky => "Inner rocky",
            CompanionRole.ShepherdGiant => "Shepherd giant",
            CompanionRole.OuterGasGiant => "Gas giant",
            CompanionRole.OuterIceGiant => "Ice giant",
            _ => role.ToString()
        };

    private static List<GalaxyFact> WorldRows(GalaxyPlacement placement)
    {
        var world = placement.World;
        return
        [
            new GalaxyFact("Radius", $"{F(world.RadiusEarth)} R\u2295"),
            new GalaxyFact("Mass", $"{F(world.MassEarth)} M\u2295"),
            new GalaxyFact("Surface gravity", $"{F(world.SurfaceGravityG)} g"),
            new GalaxyFact("Bulk iron", $"{F(world.BulkIronMassFraction * 100.0)} wt% (Earth 32.1)"),
            new GalaxyFact("Core mass", $"{F(world.CoreMassFraction * 100.0)} % (Earth 32.5)"),
            new GalaxyFact("Mean density", $"{F(world.MeanDensityEarth)} rho\u2295"),
            new GalaxyFact("Surface temperature", $"{F(world.SurfaceTemperatureK)} K"),
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

    private static List<GalaxyFact> WandererRows(LocalSystemSky localSky)
    {
        var rows = new List<GalaxyFact>
        {
            new(
                "Planets",
                localSky.Planets.Count == 0
                    ? "none"
                    : string.Join(", ", localSky.Planets.Select(static planet =>
                        $"{planet.DisplayName} ({planet.Orbit.SemiMajorAxisAu.ToString("0.##", CultureInfo.InvariantCulture)} AU)")))
        };

        foreach (var comet in localSky.Comets)
        {
            var showers = localSky.Showers.Where(shower => shower.ParentCometId == comet.Id)
                .Select(static shower => shower.DisplayName)
                .ToArray();
            var showerText = showers.Length == 0
                ? "no shower"
                : string.Join(", ", showers);
            rows.Add(new GalaxyFact(
                comet.DisplayName,
                $"period {comet.PeriodYears.ToString("0.#", CultureInfo.InvariantCulture)} yr, peak m {comet.PeakMagnitude.ToString("0.#", CultureInfo.InvariantCulture)}; {showerText}"));
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
