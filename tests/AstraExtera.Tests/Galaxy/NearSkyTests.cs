using AstraExtera.Galaxy;
using AstraTerra.Astronomy;
using Xunit;

namespace AstraExtera.Tests.Galaxy;

public sealed class NearSkyTests
{
    /// <summary>
    /// A planet world's near sky is its own moons: bodies that circulate, on their own months, with
    /// no parent giant among them. What it must never be is nothing at all standing in for Earth's
    /// moon, which is what a planet world used to get.
    /// </summary>
    [Fact]
    public void A_Planet_World_Gets_Its_Own_Moons_And_No_Parent()
    {
        var seenMoons = 0;
        var seenWorlds = 0;
        for (long seed = 1; seed <= 256; seed++)
        {
            var placement = GalaxyGenerator.Generate(seed);
            if (placement.WorldKind != ObserverWorldKind.TerrestrialPlanet)
            {
                continue;
            }

            seenWorlds++;
            var bodies = NearSky.Author(placement);
            var moons = placement.System.HomeMoons.ToDictionary(static moon => moon.Index);
            Assert.DoesNotContain(bodies, static body => body.Role != NearBodyRole.HomeMoon);
            Assert.Equal(bodies.Select(static body => body.Id).Distinct().Count(), bodies.Count);

            foreach (var body in bodies)
            {
                seenMoons++;
                var moon = moons[body.SourceIndex];

                // A moon of the observer's own world circulates, so a flat rate places it: there is
                // no parent to be penned in beside the way a sibling moon is.
                Assert.Null(body.Orbit);
                Assert.Equal(1.0, body.DiscFraction);
                Assert.Equal(0.0, body.RingOpenness);
                Assert.Equal(
                    NearSky.AngularDiameterDeg(moon.RadiusEarth, moon.OrbitalDistanceEarthRadii),
                    body.AngularDiameterDeg,
                    9);
                Assert.InRange(body.AngularDiameterDeg, NearSky.MinAngularDiameterDeg, 10.0);
                Assert.InRange(body.HourAngleDeg, 0.0, 360.0);
                Assert.InRange(
                    Math.Abs(body.DeclinationDeg),
                    0.0,
                    NearSky.MaxHomeMoonDeclinationDeg);
                Assert.Equal(
                    NearSky.HomeMoonHourAngleRateDegPerDay(moon.DayLengthDays),
                    body.HourAngleRateDegPerDay,
                    9);
            }
        }

        Assert.True(seenWorlds > 0, "no planet world in 256 seeds");
        Assert.True(seenMoons > 0, "no planet world in 256 seeds had a moon to draw");
    }

    /// <summary>
    /// A home moon keeps station with the stars less the turn it gives back each month, which is
    /// why a moon like Earth's rises about fifty minutes later every day. A month shorter than the
    /// day runs the rate negative: that moon rises in the west, the way Phobos does.
    /// </summary>
    [Theory]
    [InlineData(27.32, 346.82)]
    [InlineData(2.0, 180.0)]
    [InlineData(0.5, -360.0)]
    public void A_Home_Moon_Drifts_By_The_Turn_It_Gives_Back_Each_Month(double monthDays, double expectedRate)
    {
        Assert.Equal(expectedRate, NearSky.HomeMoonHourAngleRateDegPerDay(monthDays), 2);

        // A moon on an infinitely long month is a star: it keeps station with the sky.
        Assert.Equal(360.0, NearSky.HomeMoonHourAngleRateDegPerDay(double.PositiveInfinity), 9);
        Assert.Equal(360.0, NearSky.HomeMoonHourAngleRateDegPerDay(0.0));
    }

    /// <summary>
    /// Earth's moon is 0.52 degrees across and its month is 27.3 days, so a world with an analog of
    /// it should read like one rather than like a wall of rock or a speck.
    /// </summary>
    [Fact]
    public void A_Planet_Worlds_Moons_Are_Moon_Sized_And_On_Moon_Like_Months()
    {
        var seen = 0;
        for (long seed = 1; seed <= 256; seed++)
        {
            var placement = GalaxyGenerator.Generate(seed);
            if (placement.WorldKind != ObserverWorldKind.TerrestrialPlanet)
            {
                continue;
            }

            var world = placement.World;
            var roche = LocalSystem.RockyRocheLimitEarthRadii(world.RadiusEarth);
            foreach (var moon in placement.System.HomeMoons)
            {
                seen++;
                Assert.False(moon.Habitable);
                Assert.True(
                    moon.OrbitalDistanceEarthRadii > roche,
                    $"seed {seed}: a moon inside the Roche limit would be a ring, not a moon");
                Assert.InRange(
                    moon.DayLengthDays,
                    LocalSystem.MinHomeMoonMonthDays * 0.999,
                    LocalSystem.MaxHomeMoonMonthDays * 1.001);
                Assert.InRange(moon.RadiusEarth, 0.05, 0.45);
            }
        }

        Assert.True(seen > 0);
    }

    /// <summary>
    /// Two moons on nearly the same orbit would rise together night after night and never be told
    /// apart, so a family is spaced.
    /// </summary>
    [Fact]
    public void A_Planet_Worlds_Moons_Are_Spaced_Apart()
    {
        var seen = 0;
        for (long seed = 1; seed <= 256; seed++)
        {
            var placement = GalaxyGenerator.Generate(seed);
            if (placement.WorldKind != ObserverWorldKind.TerrestrialPlanet)
            {
                continue;
            }

            var orbits = placement.System.HomeMoons
                .Select(static moon => moon.OrbitalDistanceEarthRadii)
                .ToList();
            Assert.Equal(orbits.OrderBy(static orbit => orbit), orbits);
            for (var i = 1; i < orbits.Count; i++)
            {
                seen++;
                Assert.True(
                    orbits[i] / orbits[i - 1] >= LocalSystem.MinHomeMoonOrbitRatio - 1e-9,
                    $"seed {seed}: two moons ran nearly the same orbit");
            }
        }

        Assert.True(seen > 0, "no planet world in 256 seeds had more than one moon");
    }

    /// <summary>A moon world's family belongs to its giant, not to the world standing on it.</summary>
    [Fact]
    public void A_Moon_World_Hosts_No_Moons_Of_Its_Own()
    {
        Assert.Empty(PlacementOfKind(ObserverWorldKind.TerrestrialMoon).System.HomeMoons);
    }

    [Fact]
    public void A_Moon_World_Hangs_Its_Giant_In_One_Fixed_Spot()
    {
        var placement = PlacementOfKind(ObserverWorldKind.TerrestrialMoon);
        var bodies = NearSky.Author(placement);

        var giant = Assert.Single(bodies, static body => body.Role == NearBodyRole.ParentGiant);
        Assert.Equal(0.0, giant.HourAngleRateDegPerDay);
        Assert.InRange(Math.Abs(giant.HourAngleDeg), NearSky.MinParentHourAngleDeg, NearSky.MaxParentHourAngleDeg);
        Assert.InRange(
            Math.Abs(giant.DeclinationDeg),
            NearSky.MinParentDeclinationDeg,
            NearSky.MaxParentDeclinationDeg);

        // A giant a few dozen planet radii away is tens of degrees wide -- the whole reason it needs
        // a disc rather than a dot.
        Assert.InRange(giant.AngularDiameterDeg, 4.0, 90.0);
        Assert.InRange(giant.DiscFraction, 0.15, 1.0);
    }

    [Fact]
    public void Sibling_Moons_Drift_And_The_Home_Moon_Is_Never_One_Of_Them()
    {
        var seen = 0;
        for (long seed = 1; seed <= 256; seed++)
        {
            var placement = GalaxyGenerator.Generate(seed);
            if (placement.WorldKind != ObserverWorldKind.TerrestrialMoon)
            {
                continue;
            }

            var bodies = NearSky.Author(placement);
            var home = placement.System.Moons.Single(static moon => moon.Habitable);
            Assert.DoesNotContain(bodies, body => body.SourceIndex == home.Index && body.Role == NearBodyRole.SiblingMoon);

            foreach (var sibling in bodies.Where(static body => body.Role == NearBodyRole.SiblingMoon))
            {
                seen++;
                Assert.NotEqual(0.0, sibling.HourAngleRateDegPerDay);
                Assert.InRange(sibling.AngularDiameterDeg, NearSky.MinAngularDiameterDeg, 30.0);
                Assert.Equal(1.0, sibling.DiscFraction);
                Assert.InRange(sibling.HourAngleDeg, 0.0, 360.0);
            }

            Assert.Equal(bodies.Select(static body => body.Id).Distinct().Count(), bodies.Count);
        }

        Assert.True(seen > 0, "no moon world in 256 seeds had a sibling to draw");
    }

    /// <summary>
    /// The mean drift is the beat between the two orbits: one turn's worth of sky per synodic
    /// period -- and the sun, which is the same problem with an infinitely distant sibling, goes
    /// round once a day. Only an outer sibling actually completes that turn; see
    /// <see cref="An_Inner_Sibling_Never_Leaves_The_Giant"/>.
    /// </summary>
    [Theory]
    [InlineData(1.0, 2.0)]
    [InlineData(1.0, 0.6)]
    [InlineData(1.4, 9.0)]
    public void A_Siblings_Drift_Is_One_Turn_Per_Synodic_Period(double homePeriod, double siblingPeriod)
    {
        var rate = NearSky.HourAngleRateDegPerDay(homePeriod, siblingPeriod);
        var synodicPeriodDays = 1.0 / Math.Abs((1.0 / homePeriod) - (1.0 / siblingPeriod));

        // Rates are per world day, and one world day is one orbit of the home moon.
        Assert.Equal(360.0 / (synodicPeriodDays / homePeriod), Math.Abs(rate), 9);
    }

    [Fact]
    public void An_Inner_Sibling_Drifts_The_Other_Way_From_An_Outer_One()
    {
        var inner = NearSky.HourAngleRateDegPerDay(1.0, 0.5);
        var outer = NearSky.HourAngleRateDegPerDay(1.0, 4.0);

        Assert.True(inner < 0.0);
        Assert.True(outer > 0.0);

        // The sun is the limit of a sibling infinitely far out: one turn of the sky per day.
        Assert.Equal(360.0, NearSky.HourAngleRateDegPerDay(1.0, double.PositiveInfinity), 6);
        Assert.Equal(0.0, NearSky.HourAngleRateDegPerDay(1.0, 1.0), 9);
    }

    /// <summary>
    /// A habitable moon is a regular satellite: it orbits in its giant's equatorial plane, which is
    /// the plane the rings lie in. So an observer stands inside the ring plane and the rings are a
    /// line across the planet, not an ellipse around it -- however far over the giant is tipped.
    /// </summary>
    [Fact]
    public void The_Parent_Giants_Rings_Are_Seen_Edge_On_From_Its_Own_Moon()
    {
        var seen = 0;
        for (long seed = 1; seed <= 256; seed++)
        {
            var placement = GalaxyGenerator.Generate(seed);
            if (placement.WorldKind != ObserverWorldKind.TerrestrialMoon)
            {
                continue;
            }

            var giant = NearSky.Author(placement).Single(static body => body.Role == NearBodyRole.ParentGiant);
            seen++;

            // Two degrees of opening is a line a few pixels thick across a face tens of degrees wide.
            Assert.InRange(giant.RingOpenness, 0.0, Math.Sin(3.0 * Math.PI / 180.0));

            // A giant lying well over on its side shows an observer elsewhere in the system a wide
            // ellipse. From its own moon that same giant still shows a line, because the moon went
            // over with it.
            if (placement.System.ParentGiantAppearance is { ObliquityDeg: > 30.0 } tipped)
            {
                Assert.True(
                    giant.RingOpenness < GiantAppearances.RingOpenness(tipped) * 0.25,
                    $"seed {seed}: a tipped giant's rings opened up as if watched from another planet");
            }
        }

        Assert.True(seen > 0);
    }

    [Fact]
    public void Standing_Off_The_Equator_Lifts_The_Rings_Further_Than_The_Orbit_Does()
    {
        // A world of one Earth radius at forty radii out: about 1.4 degrees from latitude alone.
        var fromLatitudeOnly = NearSky.RingOpennessFromMoon(0.0, homeRadiusEarth: 1.0, homeOrbit: 40.0);
        var withTilt = NearSky.RingOpennessFromMoon(0.5, homeRadiusEarth: 1.0, homeOrbit: 40.0);

        Assert.Equal(Math.Sin(Math.Atan(1.0 / 40.0)), fromLatitudeOnly, 9);
        Assert.InRange(fromLatitudeOnly * 180.0 / Math.PI, 1.0, 2.0);
        Assert.True(withTilt > fromLatitudeOnly);
        Assert.Equal(0.0, NearSky.RingOpennessFromMoon(0.5, 1.0, 0.0));
    }

    [Fact]
    public void Angular_Size_Follows_Radius_Over_Distance()
    {
        // A body one distance-unit away and half a unit across covers 53 degrees.
        Assert.Equal(53.13, NearSky.AngularDiameterDeg(0.5, 1.0), 2);
        Assert.Equal(0.0, NearSky.AngularDiameterDeg(1.0, 0.0));
        Assert.True(NearSky.AngularDiameterDeg(1.0, 10.0) > NearSky.AngularDiameterDeg(1.0, 20.0));

        // The RMS separation of two coplanar circular orbits, which is what a fixed size assumes.
        Assert.Equal(Math.Sqrt(2.0), NearSky.MeanSeparationEarthRadii(1.0, 1.0), 9);
        Assert.Equal(5.0, NearSky.MeanSeparationEarthRadii(3.0, 4.0), 9);
    }

    [Fact]
    public void Authoring_Is_Deterministic_For_A_Seed()
    {
        var placement = PlacementOfKind(ObserverWorldKind.TerrestrialMoon);

        Assert.Equal(NearSky.Author(placement), NearSky.Author(placement));

        var planet = PlacementOfKind(ObserverWorldKind.TerrestrialPlanet);
        Assert.Equal(NearSky.Author(planet), NearSky.Author(planet));
    }

    /// <summary>
    /// A sibling closer in than the home moon cannot leave the giant: it swings about it out to
    /// <c>asin(q)</c> and back, the way Venus never leaves the sun. Drifting it round the sky at a
    /// flat rate -- which is what the placement used to do -- put moons in the midnight sky that
    /// physically spend their whole lives on the giant's face.
    /// </summary>
    [Fact]
    public void An_Inner_Sibling_Never_Leaves_The_Giant()
    {
        var seen = 0;
        for (long seed = 1; seed <= 256; seed++)
        {
            var placement = GalaxyGenerator.Generate(seed);
            if (placement.WorldKind != ObserverWorldKind.TerrestrialMoon)
            {
                continue;
            }

            var bodies = NearSky.Author(placement);
            var giant = bodies.Single(static body => body.Role == NearBodyRole.ParentGiant);
            foreach (var sibling in bodies.Where(static body => body.Orbit is { DistanceRatio: < 1.0 }))
            {
                seen++;
                var orbit = sibling.Orbit!;
                var greatestElongation = Math.Asin(orbit.DistanceRatio) * 180.0 / Math.PI;
                for (var step = 0; step < 360; step++)
                {
                    var elongation = NearBodyRenderModel.ElongationDeg(orbit.DistanceRatio, step);
                    Assert.InRange(Math.Abs(elongation), 0.0, greatestElongation + 1e-9);
                }

                // And it starts somewhere on that arc rather than anywhere on the sky.
                var fromGiant = Math.Abs(CelestialMath.NormalizeDegrees(sibling.HourAngleDeg - giant.HourAngleDeg + 180.0) - 180.0);
                Assert.InRange(fromGiant, 0.0, greatestElongation + 1e-9);
            }
        }

        Assert.True(seen > 0, "no moon world in 256 seeds had an inner sibling");
    }

    /// <summary>
    /// The orbit is what places a sibling, so it has to be the same orbit the generator wrote down:
    /// the ratio of the two radii, and a lead that changes at the beat between the two periods.
    /// </summary>
    [Fact]
    public void A_Siblings_Orbit_Is_The_One_The_System_Gave_It()
    {
        var placement = PlacementOfKind(ObserverWorldKind.TerrestrialMoon);
        var home = placement.System.MoonOrbitalDistanceEarthRadii!.Value;
        var moons = placement.System.Moons.ToDictionary(static moon => moon.Index);

        var siblings = NearSky.Author(placement).Where(static body => body.Role == NearBodyRole.SiblingMoon).ToList();
        Assert.NotEmpty(siblings);

        foreach (var sibling in siblings)
        {
            var moon = moons[sibling.SourceIndex];
            var orbit = Assert.IsType<SiblingOrbit>(sibling.Orbit);

            Assert.Equal(moon.OrbitalDistanceEarthRadii / home, orbit.DistanceRatio, 9);
            Assert.Equal(NearSky.PhaseRateDegPerDay(1.0, moon.DayLengthDays), orbit.PhaseRateDegPerDay, 9);
            Assert.InRange(orbit.PhaseDeg, 0.0, 360.0);

            // An inner sibling runs ahead of us, an outer one falls behind.
            Assert.Equal(orbit.DistanceRatio < 1.0, orbit.PhaseRateDegPerDay > 0.0);

            // The width is quoted at the giant's distance, so the renderer can scale it from there.
            Assert.Equal(NearSky.AngularDiameterDeg(moon.RadiusEarth, home), sibling.AngularDiameterDeg, 9);
        }
    }

    /// <summary>The giant does not move, so it has no orbit to be placed from.</summary>
    [Fact]
    public void The_Giant_Is_Placed_By_Hand_And_Not_By_An_Orbit()
    {
        var bodies = NearSky.Author(PlacementOfKind(ObserverWorldKind.TerrestrialMoon));

        Assert.Null(bodies.Single(static body => body.Role == NearBodyRole.ParentGiant).Orbit);
    }

    /// <summary>
    /// The mean drift is still the beat between the orbits -- it is what a sibling averages over a
    /// synodic period -- and it runs the opposite way from the lead that produces it.
    /// </summary>
    [Fact]
    public void The_Mean_Drift_Is_The_Lead_Rate_Turned_Round()
    {
        Assert.Equal(-NearSky.PhaseRateDegPerDay(1.0, 4.0), NearSky.HourAngleRateDegPerDay(1.0, 4.0), 9);
        Assert.Equal(360.0 * (2.0 - 1.0), NearSky.PhaseRateDegPerDay(1.0, 0.5), 9);
        Assert.Equal(0.0, NearSky.PhaseRateDegPerDay(1.0, 0.0));
        Assert.Equal(0.0, NearSky.PhaseRateDegPerDay(0.0, 1.0));
    }

    private static GalaxyPlacement PlacementOfKind(ObserverWorldKind kind)
    {
        for (long seed = 1; seed <= 400; seed++)
        {
            var placement = GalaxyGenerator.Generate(seed);
            if (placement.WorldKind == kind)
            {
                return placement;
            }
        }

        throw new InvalidOperationException($"No seed in 400 produced a {kind}.");
    }
}
