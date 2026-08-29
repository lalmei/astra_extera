namespace AstraExtera.Galaxy;

/// <summary>What a near body is: the world's parent giant, or one of its sibling moons.</summary>
public enum NearBodyRole
{
    ParentGiant = 0,
    SiblingMoon = 1
}

/// <summary>
/// One body close enough to show a disc, placed for the tidally locked world that watches it.
/// </summary>
/// <param name="AngularDiameterDeg">
/// The whole drawn face, rings included, as seen from the observer's world.
/// </param>
/// <param name="DiscFraction">The globe itself as a fraction of that face; 1 when there are no rings.</param>
/// <param name="HourAngleDeg">Where on the sky it hangs, measured west from the meridian.</param>
/// <param name="HourAngleRateDegPerDay">
/// How fast it drifts across the sky in world days. Zero for the parent giant, which a locked world
/// keeps in one place; 360 would be the rate of the sun.
/// </param>
public sealed record NearBody(
    string Id,
    NearBodyRole Role,
    int SourceIndex,
    string DisplayName,
    double AngularDiameterDeg,
    double DiscFraction,
    double HourAngleDeg,
    double HourAngleRateDegPerDay,
    double DeclinationDeg,
    double Brightness);

/// <summary>
/// The sky of a world that is itself a moon: the giant it orbits, and its sibling moons.
/// </summary>
/// <remarks>
/// <para>
/// A habitable moon is tidally locked to its giant -- that is what gives it a day at all, since one
/// orbit is one day. From the ground the consequence is stark: the giant never rises and never sets.
/// It hangs at one spot forever, going through its phases as the sun goes round, full near local
/// midnight and dark at noon. Everything else in this sky moves; that one thing does not.
/// </para>
/// <para>
/// Sibling moons drift past it at the rate the two orbits beat against each other. An inner sibling
/// laps the observer and slides one way, an outer one falls behind and slides the other, and the sun
/// is the limiting case of an infinitely distant sibling that goes round once a day.
/// </para>
/// <para>
/// Nothing here draws. The geometry is worked out from the stored placement so it can be tested
/// without a game, and the faces are painted separately, where Cairo is available.
/// </para>
/// </remarks>
public static class NearSky
{
    /// <summary>Faces smaller than this are not worth a disc; the planet catalog draws them as points.</summary>
    public const double MinAngularDiameterDeg = 0.05;

    /// <summary>
    /// How far off the meridian the giant hangs. Straight overhead would put it on the sun's noon
    /// track and eclipse the sun every single day, which says more about the model than about the
    /// world, so the orbit is authored with a tilt and the giant sits off to one side.
    /// </summary>
    public const double MinParentHourAngleDeg = 22.0;
    public const double MaxParentHourAngleDeg = 58.0;

    public const double MinParentDeclinationDeg = 8.0;
    public const double MaxParentDeclinationDeg = 26.0;

    public static IReadOnlyList<NearBody> Author(GalaxyPlacement placement)
    {
        ArgumentNullException.ThrowIfNull(placement);
        if (placement.WorldKind != ObserverWorldKind.TerrestrialMoon)
        {
            return [];
        }

        var system = placement.System;
        if (system.ParentGiantMassEarth is not { } giantMass
            || system.MoonOrbitalDistanceEarthRadii is not { } homeOrbit
            || homeOrbit <= 0.0)
        {
            return [];
        }

        var rng = new SplitMix64(MixSeed(placement.WorldSeed, 0x4D00));
        var giantRadius = LocalSystem.GiantRadiusEarthRadii(giantMass);
        var ringOuter = system.ParentGiantAppearance?.Ring?.OuterRadiusPlanetRadii ?? 1.0;
        var hourAngle = SignedSpread(ref rng, MinParentHourAngleDeg, MaxParentHourAngleDeg);
        var declination = SignedSpread(ref rng, MinParentDeclinationDeg, MaxParentDeclinationDeg);

        var bodies = new List<NearBody>(system.Moons.Length + 1)
        {
            new(
                "parent-giant",
                NearBodyRole.ParentGiant,
                SourceIndex: 0,
                DisplayName: "the giant",
                AngularDiameterDeg: AngularDiameterDeg(giantRadius * ringOuter, homeOrbit),
                DiscFraction: 1.0 / Math.Max(1.0, ringOuter),
                hourAngle,
                HourAngleRateDegPerDay: 0.0,
                declination,
                Brightness: 0.92)
        };

        var homePeriod = HomePeriodDays(system);
        foreach (var moon in system.Moons)
        {
            if (moon.Habitable)
            {
                continue;
            }

            var separation = MeanSeparationEarthRadii(homeOrbit, moon.OrbitalDistanceEarthRadii);
            var angular = AngularDiameterDeg(moon.RadiusEarth, separation);
            if (angular < MinAngularDiameterDeg)
            {
                continue;
            }

            bodies.Add(new NearBody(
                $"sibling-moon-{moon.Index}",
                NearBodyRole.SiblingMoon,
                moon.Index,
                moon.DisplayName,
                angular,
                DiscFraction: 1.0,
                HourAngleDeg: rng.NextRange(0.0, 360.0),
                HourAngleRateDegPerDay: HourAngleRateDegPerDay(homePeriod, moon.DayLengthDays),
                DeclinationDeg: declination + rng.NextRange(-6.0, 6.0),
                Brightness: rng.NextRange(0.40, 0.62)));
        }

        return bodies;
    }

    /// <summary>Apparent diameter of a body of <paramref name="radius"/> at <paramref name="distance"/>.</summary>
    public static double AngularDiameterDeg(double radius, double distance)
        => distance <= 0.0 ? 0.0 : 2.0 * Math.Atan(radius / distance) * 180.0 / Math.PI;

    /// <summary>
    /// How fast a sibling drifts across the locked world's sky, in degrees per world day.
    /// </summary>
    /// <remarks>
    /// One world day is one orbit of the giant, so the observer's own frame turns once a day. A
    /// sibling's direction turns at its own orbital rate, and what reaches the sky is the difference:
    /// zero for a body that keeps station with us, and the full 360 a day for something infinitely
    /// far off -- which is exactly what the sun does.
    /// </remarks>
    public static double HourAngleRateDegPerDay(double homePeriodDays, double siblingPeriodDays)
    {
        if (homePeriodDays <= 0.0 || siblingPeriodDays <= 0.0)
        {
            return 0.0;
        }

        return 360.0 * (1.0 - (homePeriodDays / siblingPeriodDays));
    }

    /// <summary>
    /// Typical distance between two moons on coplanar circular orbits: the root-mean-square over a
    /// full synodic cycle, which is what a body drawn at one fixed size should be drawn at.
    /// </summary>
    public static double MeanSeparationEarthRadii(double homeOrbit, double siblingOrbit)
        => Math.Sqrt((homeOrbit * homeOrbit) + (siblingOrbit * siblingOrbit));

    private static double HomePeriodDays(LocalSystem system)
        => system.MoonDayLengthDays is { } day && day > 0.0 ? day : 1.0;

    /// <summary>A value in the band, on one side of zero or the other.</summary>
    private static double SignedSpread(ref SplitMix64 rng, double min, double max)
    {
        var magnitude = rng.NextRange(min, max);
        return rng.NextBool(0.5) ? magnitude : -magnitude;
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
