using AstraExtera.Galaxy;
using Xunit;

namespace AstraExtera.Tests.Galaxy;

public sealed class NearSkyTests
{
    [Fact]
    public void A_Planet_World_Has_No_Near_Bodies()
    {
        var placement = PlacementOfKind(ObserverWorldKind.TerrestrialPlanet);

        Assert.Empty(NearSky.Author(placement));
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
    /// The drift rate is the beat between the two orbits, so a sibling goes right round the sky once
    /// per synodic period -- and the sun, which is the same problem with an infinitely distant
    /// sibling, goes round once a day.
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
