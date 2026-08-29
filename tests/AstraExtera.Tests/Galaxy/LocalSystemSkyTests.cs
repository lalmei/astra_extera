using AstraExtera.Galaxy;
using Xunit;

namespace AstraExtera.Tests.Galaxy;

public sealed class LocalSystemSkyTests
{
    [Fact]
    public void Every_Companion_Becomes_A_Naked_Eye_Planet()
    {
        var sky = GalaxySky.Author(42);

        Assert.Equal(sky.Placement.System.Companions.Length, sky.LocalSky.Planets.Count);
        Assert.Equal(
            sky.Placement.System.Companions.Select(static body => body.SemiMajorAxisAu),
            sky.LocalSky.Planets.Select(static planet => planet.Orbit.SemiMajorAxisAu));
        Assert.Equal(sky.Placement.System.OrbitalDistanceAu, sky.LocalSky.Observer.SemiMajorAxisAu);
        Assert.Equal(0.0, sky.LocalSky.Observer.InclinationDeg);
    }

    [Fact]
    public void A_One_Au_Year_Advances_360_Degrees_Per_World_Year()
    {
        Assert.Equal(36000.0, LocalSystemSky.MeanLongitudeRateDegPerCentury(365.25), 6);
    }

    [Fact]
    public void Authoring_Is_Deterministic_For_A_Seed()
    {
        var first = LocalSystemSky.Author(GalaxyGenerator.Generate(42));
        var second = LocalSystemSky.Author(GalaxyGenerator.Generate(42));

        Assert.Equal(first.Planets.Select(static p => p.Id), second.Planets.Select(static p => p.Id));
        Assert.Equal(first.Comets.Select(static c => c.Id), second.Comets.Select(static c => c.Id));
        Assert.Equal(first.Showers.Select(static s => s.Id), second.Showers.Select(static s => s.Id));
        Assert.Equal(first.Comets[0].Path, second.Comets[0].Path);
    }

    [Fact]
    public void Every_Comet_Leaves_A_Meteor_Shower_On_The_Observers_Orbit()
    {
        for (long seed = 1; seed <= 40; seed++)
        {
            var sky = LocalSystemSky.Author(GalaxyGenerator.Generate(seed));

            Assert.InRange(sky.Comets.Count, 2, 4);
            Assert.All(sky.Comets, comet =>
            {
                Assert.Equal(-1.0, comet.Path[0].Phase);
                Assert.Contains(comet.Path, static keyframe => keyframe.Phase == 0.0);
                Assert.Equal(1.0, comet.Path[^1].Phase);
                Assert.True(comet.PeriodYears > 1.0);
                Assert.Contains(sky.Showers, shower => shower.ParentCometId == comet.Id);
            });

            foreach (var shower in sky.Showers)
            {
                Assert.InRange(shower.RightAscensionDeg, 0.0, 360.0);
                Assert.InRange(shower.DeclinationDeg, -90.0, 90.0);
                Assert.InRange(shower.PeakSolarLongitudeDeg, 0.0, 360.0);
                Assert.InRange(shower.WindowHalfWidthDeg, 0.1, 180.0);
                Assert.True(shower.PeakZenithHourlyRate > 0.0);
            }
        }
    }

    [Fact]
    public void A_Halley_Type_Comet_Leaves_Both_Nodes()
    {
        LocalSystemSky? found = null;
        for (long seed = 1; seed <= 200 && found is null; seed++)
        {
            var sky = LocalSystemSky.Author(GalaxyGenerator.Generate(seed));
            if (sky.Comets.Any(static comet => comet.PeriodYears >= 45.0))
            {
                found = sky;
            }
        }

        Assert.NotNull(found);
        var halley = found.Comets.First(static comet => comet.PeriodYears >= 45.0);
        Assert.Equal(2, found.Showers.Count(shower => shower.ParentCometId == halley.Id));
    }

    [Fact]
    public void The_Stored_Local_Sky_Round_Trips()
    {
        var original = GalaxySky.Author(42).LocalSky;
        var restored = LocalSystemSkyCodec.FromUtf8(LocalSystemSkyCodec.ToUtf8(original));

        Assert.Equal(original.Observer, restored.Observer);
        Assert.Equal(original.Planets.Count, restored.Planets.Count);
        Assert.Equal(original.Planets[0].Id, restored.Planets[0].Id);
        Assert.Equal(original.Comets.Select(static c => c.DisplayName), restored.Comets.Select(static c => c.DisplayName));
        Assert.Equal(original.Showers.Select(static s => s.Id), restored.Showers.Select(static s => s.Id));
    }

    [Fact]
    public void A_Stored_Local_Sky_Is_Not_Resampled()
    {
        var placement = GalaxyGenerator.Generate(42);
        var authored = LocalSystemSky.Author(placement);
        var fake = authored with
        {
            Comets = []
        };

        var resolution = GalaxySkyStore.Resolve(placement, GalaxySky.Author(placement).StarField, 99, fake);

        Assert.False(resolution.LocalSkyDirty);
        Assert.Empty(resolution.Sky.LocalSky.Comets);
        Assert.NotEmpty(authored.Comets);
    }
}
