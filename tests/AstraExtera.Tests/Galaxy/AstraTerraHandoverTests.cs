using AstraExtera.Galaxy;
using AstraExtera.Sync;
using AstraTerra.Astronomy;
using Xunit;

namespace AstraExtera.Tests.Galaxy;

/// <summary>
/// Checks the handover against AstraTerra's real types rather than against a copy of its field
/// names. System.Text.Json binds these records positionally through the constructor and leaves any
/// parameter it cannot match at its default, so a renamed field would not throw -- it would quietly
/// produce a sky of magnitude-zero stars at the celestial origin.
/// </summary>
public sealed class AstraTerraHandoverTests
{
    [Fact]
    public void Exported_Stars_Fill_Every_Field_AstraTerra_Reads()
    {
        var placement = GalaxyGenerator.Generate(42);
        var entries = StarCatalogExport.BuildEntries(placement, StarFieldSampler.Sample(placement));

        var stars = entries
            .Select(entry => new StarCatalogEntry(
                entry.Hip,
                entry.RightAscensionDeg,
                entry.DeclinationDeg,
                entry.VisualMagnitude,
                entry.BvColorIndex,
                entry.IsGuideStar))
            .ToList();

        var catalog = new StarCatalog(stars, [], [], []);

        Assert.Equal(entries.Count, catalog.Stars.Count);
        Assert.Empty(catalog.GuideGroups);
        Assert.Empty(catalog.SkyCultures);
        Assert.Empty(catalog.DeepSkyObjects);

        // Nothing left at a default: a silently unbound field would show up as a zero here.
        Assert.All(catalog.Stars, star =>
        {
            Assert.True(star.Hip > 0);
            Assert.NotNull(star.BvColorIndex);
            Assert.NotEqual(0.0, star.DeclinationDeg);
        });
        Assert.Contains(catalog.Stars, star => star.IsGuideStar);
        Assert.Contains(catalog.Stars, star => star.VisualMagnitude < 0.0);
    }

    /// <summary>
    /// AstraTerra projects stars through its own sky model, so the exported positions have to be
    /// values it can actually place: every star should be visible from some latitude at some hour.
    /// </summary>
    [Fact]
    public void Exported_Stars_Project_Through_AstraTerras_Sky_Model()
    {
        var placement = GalaxyGenerator.Generate(42);
        var entries = StarCatalogExport.BuildEntries(
            placement,
            StarFieldSampler.Sample(placement, new StarFieldOptions { ResolvedStarBudget = 400 }));

        var projectedSomewhere = 0;
        foreach (var entry in entries)
        {
            var star = new StarCatalogEntry(
                entry.Hip,
                entry.RightAscensionDeg,
                entry.DeclinationDeg,
                entry.VisualMagnitude,
                entry.BvColorIndex,
                entry.IsGuideStar);

            for (var latitude = -60.0; latitude <= 60.0; latitude += 30.0)
            {
                for (var sidereal = 0.0; sidereal < 360.0; sidereal += 45.0)
                {
                    if (StarRenderModel.Project(star, latitude, sidereal, brightnessBias: 1.0) is not null)
                    {
                        projectedSomewhere++;
                        goto next;
                    }
                }
            }

        next: ;
        }

        Assert.Equal(entries.Count, projectedSomewhere);
    }

    [Fact]
    public void Exported_Planets_Fill_Every_Field_AstraTerra_Reads()
    {
        var sky = GalaxySky.Author(42);
        var catalog = LocalSystemSkyExport.ToPlanetCatalog(sky.LocalSky);

        Assert.Equal(sky.LocalSky.Planets.Count, catalog.Planets.Count);
        Assert.Equal(sky.LocalSky.Observer.SemiMajorAxisAu, catalog.Observer.SemiMajorAxisAu);
        Assert.All(catalog.Planets, planet =>
        {
            Assert.False(string.IsNullOrWhiteSpace(planet.Id));
            Assert.False(string.IsNullOrWhiteSpace(planet.DisplayName));
            Assert.True(planet.Elements.SemiMajorAxisAu > 0.0);
            Assert.True(planet.Elements.MeanLongitudeRateDegPerCentury > 0.0);
        });

        var sample = catalog.Planets[0];
        var ephemeris = new PlanetEphemeris(sample, catalog.Observer, daysPerYear: 360);
        var position = ephemeris.PositionAt(0.0);
        Assert.InRange(position.RightAscensionDeg, 0.0, 360.0);
        Assert.InRange(position.DeclinationDeg, -90.0, 90.0);
        Assert.True(double.IsFinite(ephemeris.MagnitudeAt(0.0)));
    }

    [Fact]
    public void Exported_Comets_And_Showers_Fill_Every_Field_AstraTerra_Reads()
    {
        var sky = GalaxySky.Author(42);
        var comets = LocalSystemSkyExport.ToCometCatalog(sky.LocalSky);
        var showers = LocalSystemSkyExport.ToMeteorShowers(sky.LocalSky);

        Assert.Equal(sky.LocalSky.Comets.Count, comets.Comets.Count);
        Assert.Equal(sky.LocalSky.Showers.Count, showers.Count);
        Assert.All(comets.Comets, comet =>
        {
            Assert.True(comet.Path.Count >= 3);
            Assert.Equal(-1.0, comet.Path[0].Phase);
            Assert.Equal(1.0, comet.Path[^1].Phase);
            var ephemeris = new CometEphemeris(comet, daysPerYear: 360);
            var atPerihelion = comet.FirstPerihelionYear * 360.0;
            var position = ephemeris.PositionAt(atPerihelion);
            Assert.InRange(position.RightAscensionDeg, 0.0, 360.0);
            Assert.True(ephemeris.ApparitionAt(atPerihelion).IsVisible);
        });
        Assert.All(showers, shower =>
        {
            Assert.False(string.IsNullOrWhiteSpace(shower.Id));
            Assert.InRange(shower.PeakSolarLongitudeDeg, 0.0, 360.0);
            Assert.True(shower.PeakZenithHourlyRate > 0.0);
        });
    }
}
