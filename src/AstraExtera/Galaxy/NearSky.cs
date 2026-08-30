using AstraTerra.Astronomy;

namespace AstraExtera.Galaxy;

/// <summary>What a near body is: the world's parent giant, or one of its sibling moons.</summary>
public enum NearBodyRole
{
    ParentGiant = 0,
    SiblingMoon = 1
}

/// <summary>
/// The orbit a sibling moon is on, for the bodies that go round the giant rather than hanging still.
/// </summary>
/// <param name="AnchorHourAngleDeg">Where the giant hangs, which is what the sibling is placed against.</param>
/// <param name="DistanceRatio">The sibling's orbit over the home moon's, both about the giant.</param>
/// <param name="PhaseDeg">How far ahead of home the sibling sits on its orbit at day zero.</param>
/// <param name="PhaseRateDegPerDay">
/// How fast that lead changes per world day: <c>360 * (homePeriod / siblingPeriod - 1)</c>.
/// </param>
public sealed record SiblingOrbit(
    double AnchorHourAngleDeg,
    double DistanceRatio,
    double PhaseDeg,
    double PhaseRateDegPerDay);

/// <summary>
/// One body close enough to show a disc, placed for the tidally locked world that watches it.
/// </summary>
/// <param name="AngularDiameterDeg">
/// The whole drawn face, rings included, as seen from the observer's world. For a sibling this is
/// the width at the giant's own distance; the drawn width follows the distance from there.
/// </param>
/// <param name="DiscFraction">The globe itself as a fraction of that face; 1 when there are no rings.</param>
/// <param name="HourAngleDeg">Where on the sky it hangs at day zero, measured west from the meridian.</param>
/// <param name="HourAngleRateDegPerDay">
/// How fast it drifts across the sky in world days, averaged over a synodic period. Zero for the
/// parent giant, which a locked world keeps in one place; 360 would be the rate of the sun. A
/// sibling's real motion is not this flat rate -- see <paramref name="Orbit"/>.
/// </param>
/// <param name="Orbit">
/// The orbit a sibling is actually placed from, and null for the giant, which does not move. It
/// supersedes the fixed hour angle and rate above, which then only say where the body starts and
/// how fast it comes round on average.
/// </param>
/// <param name="RingOpenness">
/// How far open this body's rings look from here, as the ellipse's short axis over its long one. Near
/// zero for a parent giant, because a locked world sits in its ring plane.
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
    double Brightness,
    double RingOpenness,
    SiblingOrbit? Orbit = null);

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
/// Sibling moons drift past it at the rate the two orbits beat against each other -- an inner one
/// laps the observer, an outer one falls behind -- but only the outer ones go right round the sky.
/// A sibling closer in than the observer is bound to the giant the way Venus is bound to the sun:
/// its direction swings back and forth about the giant out to an elongation of <c>asin(q)</c> and no
/// further, so a moon well inside the observer's orbit lives out its whole life on the giant's face
/// and rings, transiting it and passing behind it. The sun is the far limit of the same geometry: an
/// infinitely distant sibling, going round once a day.
/// </para>
/// <para>
/// That is why a sibling carries its orbit rather than a drift rate. A rate can only circulate, and
/// half these bodies do not.
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

    /// <summary>
    /// How far the world's orbit is tilted from its giant's equator, which is also its ring plane.
    /// A habitable moon is a regular satellite -- it formed in the disc that became the rings, which
    /// is why it is locked at all -- so this is a fraction of a degree, the way Io sits 0.04 degrees
    /// off Jupiter's equator and Europa 0.47.
    /// </summary>
    public const double MinOrbitInclinationDeg = 0.03;
    public const double MaxOrbitInclinationDeg = 0.6;

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
        var inclination = rng.NextRange(MinOrbitInclinationDeg, MaxOrbitInclinationDeg);
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
                Brightness: 0.92,
                RingOpenness: RingOpennessFromMoon(inclination, homeRadiusEarth: placement.World.RadiusEarth, homeOrbit))
        };

        var homePeriod = HomePeriodDays(system);
        foreach (var moon in system.Moons)
        {
            if (moon.Habitable)
            {
                continue;
            }

            // Whether a body is worth a disc at all is asked of a typical day rather than of the
            // day it passes closest, so the set of moons drawn does not depend on where they
            // happen to stand.
            var separation = MeanSeparationEarthRadii(homeOrbit, moon.OrbitalDistanceEarthRadii);
            if (AngularDiameterDeg(moon.RadiusEarth, separation) < MinAngularDiameterDeg)
            {
                continue;
            }

            var orbit = new SiblingOrbit(
                hourAngle,
                DistanceRatio: moon.OrbitalDistanceEarthRadii / homeOrbit,
                PhaseDeg: rng.NextRange(0.0, 360.0),
                PhaseRateDegPerDay: PhaseRateDegPerDay(homePeriod, moon.DayLengthDays));

            bodies.Add(new NearBody(
                $"sibling-moon-{moon.Index}",
                NearBodyRole.SiblingMoon,
                moon.Index,
                moon.DisplayName,
                // Drawn at the giant's distance and scaled from there, so the swing in how far off
                // a sibling is reaches the size it draws at.
                AngularDiameterDeg(moon.RadiusEarth, homeOrbit),
                DiscFraction: 1.0,
                HourAngleDeg: StartingHourAngleDeg(orbit),
                HourAngleRateDegPerDay: HourAngleRateDegPerDay(homePeriod, moon.DayLengthDays),
                // Coplanar to a fraction of a degree, so a sibling runs along the giant's own ring
                // line rather than wandering off it.
                DeclinationDeg: declination + rng.NextRange(-inclination, inclination),
                Brightness: rng.NextRange(0.40, 0.62),
                RingOpenness: 0.0,
                orbit));
        }

        return bodies;
    }

    /// <summary>
    /// How far open the parent giant's rings look from its own moon -- which is barely at all.
    /// </summary>
    /// <remarks>
    /// The world orbits in the giant's equatorial plane, and the rings lie in that same plane, so an
    /// observer is inside the ring plane and sees the rings edge-on: a line across the planet rather
    /// than an ellipse around it. Only two things lift them off that line, and both are small. The
    /// orbit's own tilt swings the view by its inclination twice a day, and standing away from the
    /// world's equator lifts the observer up to one world radius clear of the plane -- which at a few
    /// dozen radii out is a degree or so, and is usually the larger of the two.
    /// </remarks>
    public static double RingOpennessFromMoon(double inclinationDeg, double homeRadiusEarth, double homeOrbit)
    {
        if (homeOrbit <= 0.0)
        {
            return 0.0;
        }

        var fromLatitude = Math.Atan(homeRadiusEarth / homeOrbit);
        var fromInclination = inclinationDeg * Math.PI / 180.0;
        return Math.Clamp(Math.Sin(fromLatitude + fromInclination), 0.0, 1.0);
    }

    /// <summary>Apparent diameter of a body of <paramref name="radius"/> at <paramref name="distance"/>.</summary>
    public static double AngularDiameterDeg(double radius, double distance)
        => distance <= 0.0 ? 0.0 : 2.0 * Math.Atan(radius / distance) * 180.0 / Math.PI;

    /// <summary>
    /// How fast a sibling drifts across the locked world's sky on average, in degrees per world day.
    /// </summary>
    /// <remarks>
    /// One world day is one orbit of the giant, so the observer's own frame turns once a day. A
    /// sibling's direction turns at its own orbital rate, and what reaches the sky is the difference:
    /// zero for a body that keeps station with us, and the full 360 a day for something infinitely
    /// far off -- which is exactly what the sun does. This is the average of that over a synodic
    /// period, and only an infinitely distant sibling actually holds it from moment to moment: what
    /// places a sibling is its orbit.
    /// </remarks>
    public static double HourAngleRateDegPerDay(double homePeriodDays, double siblingPeriodDays)
        => -PhaseRateDegPerDay(homePeriodDays, siblingPeriodDays);

    /// <summary>
    /// How fast a sibling pulls ahead of the observer on its own orbit, in degrees per world day.
    /// </summary>
    /// <remarks>
    /// This, not the mean drift above, is what places a sibling. An inner one runs ahead and an
    /// outer one falls behind, and turning that lead into a direction on the sky is the step that
    /// keeps an inner sibling pinned to the giant instead of sending it round the whole sky.
    /// </remarks>
    public static double PhaseRateDegPerDay(double homePeriodDays, double siblingPeriodDays)
    {
        if (homePeriodDays <= 0.0 || siblingPeriodDays <= 0.0)
        {
            return 0.0;
        }

        return 360.0 * ((homePeriodDays / siblingPeriodDays) - 1.0);
    }

    /// <summary>Where an orbiting sibling stands at day zero, measured west from the meridian.</summary>
    public static double StartingHourAngleDeg(SiblingOrbit orbit)
    {
        ArgumentNullException.ThrowIfNull(orbit);
        var elongation = NearBodyRenderModel.ElongationDeg(orbit.DistanceRatio, orbit.PhaseDeg);
        return CelestialMath.NormalizeDegrees(orbit.AnchorHourAngleDeg + elongation);
    }

    /// <summary>
    /// Typical distance between two moons on coplanar circular orbits: the root-mean-square over a
    /// full synodic cycle, which is the distance to ask whether a body is worth drawing at.
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
