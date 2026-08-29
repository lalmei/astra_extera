using AstraExtera.Galaxy;
using Xunit;

namespace AstraExtera.Tests.Galaxy;

public sealed class LocalSystemTests
{
    [Fact]
    public void Mass_Luminosity_And_Habitable_Zone_Match_The_Sun()
    {
        Assert.Equal(1.0, LocalSystem.MassLuminosity(1.0), 3);
        Assert.Equal(10.0, LocalSystem.StarLifespan(1.0), 3);
        Assert.Equal(1.0, LocalSystem.ComputeStarRadiusSolar(1.0), 2);

        var (inner, outer) = LocalSystem.HabitableZone(1.0);
        Assert.Equal(0.953, inner, 2);
        Assert.Equal(1.373, outer, 2);
        Assert.Equal(2.7, LocalSystem.SnowLine(1.0), 2);
    }

    [Theory]
    [InlineData(StarSpectralClass.M, 0.08, 0.45)]
    [InlineData(StarSpectralClass.K, 0.45, 0.80)]
    [InlineData(StarSpectralClass.G, 0.80, 1.04)]
    [InlineData(StarSpectralClass.F, 1.04, 1.40)]
    public void Spectral_Class_Mass_Ranges_Cover_The_Main_Sequence(
        StarSpectralClass starClass,
        double min,
        double max)
    {
        var (lo, hi) = LocalSystem.MassRange(starClass);
        Assert.Equal(min, lo, 3);
        Assert.Equal(max, hi, 3);
    }

    [Fact]
    public void Every_Authored_System_Keeps_The_Earth_Analog_In_The_Liquid_Water_Belt()
    {
        var starClasses = new HashSet<StarSpectralClass>();
        var moons = 0;
        var planets = 0;

        for (long seed = 1; seed <= 256; seed++)
        {
            var placement = GalaxyGenerator.Generate(seed);
            var system = placement.System;
            starClasses.Add(system.StarClass);

            Assert.True(
                system.IsHabitable,
                $"seed {seed}: {string.Join("; ", system.Checks.Where(static c => !c.Passed).Select(static c => c.Detail))}");
            Assert.InRange(system.OrbitalDistanceAu, system.HabitableZoneInnerAu, system.HabitableZoneOuterAu);
            Assert.True(system.StarLifespanGyr >= LocalSystem.MinStarLifespanGyr);
            Assert.True(EarthAnalog.IsEarthlike(placement.World));

            var shepherd = Assert.Single(system.Companions, static body => body.Role == CompanionRole.ShepherdGiant);
            Assert.True(shepherd.SemiMajorAxisAu > system.SnowLineAu);
            Assert.True(shepherd.SemiMajorAxisAu > system.HabitableZoneOuterAu);

            if (placement.WorldKind == ObserverWorldKind.TerrestrialMoon)
            {
                moons++;
                Assert.NotNull(system.ParentGiantMassEarth);
                Assert.NotNull(system.MoonDayLengthDays);
                Assert.True(system.MoonDayLengthDays <= LocalSystem.MaxMoonDayDays);
                Assert.True(system.MoonDayLengthDays >= LocalSystem.MinMoonDayDays);
                Assert.NotNull(system.MoonOrbitalDistanceEarthRadii);
                Assert.NotNull(system.RocheLimitEarthRadii);
                Assert.True(system.MoonOrbitalDistanceEarthRadii > system.RocheLimitEarthRadii);
                Assert.True(system.Moons.Length >= 1);
                Assert.Equal(1, system.Moons.Count(static moon => moon.Habitable));
                Assert.Equal(system.HabitableMoonIndex, system.Moons.Single(static moon => moon.Habitable).Index);
                var home = system.Moons.Single(static moon => moon.Habitable);
                Assert.Equal(placement.World.MassEarth, home.MassEarth);
                Assert.Equal(placement.World.RadiusEarth, home.RadiusEarth);
            }
            else
            {
                planets++;
                Assert.Empty(system.Moons);
                Assert.Null(system.ParentGiantMassEarth);
                Assert.True(system.OrbitalPeriodDays >= LocalSystem.MinPlanetYearDays);
                Assert.NotEqual(StarSpectralClass.M, system.StarClass);
            }
        }

        Assert.True(planets > 0);
        Assert.True(moons > 0);
        Assert.Contains(StarSpectralClass.G, starClasses);
        Assert.Contains(StarSpectralClass.K, starClasses);
    }

    [Fact]
    public void Zone_And_System_Figures_Keep_The_World_On_The_Page()
    {
        foreach (var seed in new long[] { 42, 7, 1979, 24 })
        {
            var placement = GalaxyGenerator.Generate(seed);
            var html = GalaxyDebugHtml.Render(placement, StarFieldSampler.Sample(placement));

            Assert.Contains("Habitable zone", html, StringComparison.Ordinal);
            Assert.Contains("Full system", html, StringComparison.Ordinal);
            Assert.Contains("url(#zone-star)", html, StringComparison.Ordinal);
            Assert.Contains("url(#system-star)", html, StringComparison.Ordinal);

            foreach (var zoneView in new[] { true, false })
            {
                var maxAu = LocalSystemGeometry.MaxAu(placement.System, zoneView);
                var radius = LocalSystemGeometry.RadiusPx(placement.System.OrbitalDistanceAu, maxAu);
                var point = LocalSystemGeometry.PointOnOrbit(
                    radius,
                    zoneView ? LocalSystemGeometry.ZoneWorldAngleRad : LocalSystemGeometry.SystemWorldAngleRad);
                Assert.InRange(point.X, 0.0, LocalSystemGeometry.ViewWidth);
                Assert.InRange(point.Y, 0.0, LocalSystemGeometry.ViewHeight);
                Assert.InRange(radius, 0.0, LocalSystemGeometry.MaxRadiusPx);
            }
        }
    }
}
