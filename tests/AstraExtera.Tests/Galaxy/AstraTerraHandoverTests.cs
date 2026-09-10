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
        var starField = StarFieldSampler.Sample(placement);
        var entries = StarCatalogExport.BuildEntries(placement, starField);

        var catalog = StarCatalogHandover.ToCatalog(placement, starField);

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
        var catalog = StarCatalogHandover.ToCatalog(
            placement,
            StarFieldSampler.Sample(placement, new StarFieldOptions { ResolvedStarBudget = 400 }));

        var projectedSomewhere = 0;
        foreach (var star in catalog.Stars)
        {
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

        Assert.Equal(catalog.Stars.Count, projectedSomewhere);
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

    /// <summary>
    /// The seam this mod lives on. AstraExtera authors a locked world's giant at one hour angle and
    /// AstraTerra adds the observer's longitude to it, so the two together have to produce a giant
    /// that is fixed to the ground rather than to the player: it sinks westward as the player travels
    /// east, and on the far side of the moon it is gone. Compiling against an AstraTerra that placed
    /// near bodies by sidereal angle alone would silently give every observer the same sky.
    /// </summary>
    [Fact]
    public void A_Locked_Worlds_Giant_Is_Fixed_To_The_Ground_And_Not_To_The_Player()
    {
        var placement = MoonWorld();
        var giant = NearSky.Author(placement).Single(static body => body.Role == NearBodyRole.ParentGiant);
        var entry = Entry(giant);
        var sun = new SkyDirection(0.0, -1.0, 0.0);

        // Chosen so the giant starts well up: its authored hour angle is signed, and the latitude
        // has to be on the same side of the equator as its declination for it to clear the horizon.
        var latitude = Math.Sign(giant.DeclinationDeg) * 15.0;
        var atHome = NearBodyRenderModel.Place(entry, 0.0, latitude, 0.0, sun, 0.0);
        Assert.NotNull(atHome);
        Assert.True(atHome.AltitudeDeg > 10.0, $"the giant only reached {atHome.AltitudeDeg:0.0} deg at home");

        // Walk the whole world in the direction that carries the giant down, and it goes: an
        // observer on the far side never sees the planet their world is locked to.
        var step = -Math.Sign(giant.HourAngleDeg) * 15.0;
        var setSomewhere = false;
        for (var longitude = step; Math.Abs(longitude) <= 180.0; longitude += step)
        {
            var placed = NearBodyRenderModel.Place(entry, 0.0, latitude, longitude, sun, longitude);
            setSomewhere |= placed is null;
        }

        Assert.True(setSomewhere, "the giant stayed up at every longitude on the world");

        // And time alone is not what did it: standing still, it does not move all day.
        var sixHoursOn = NearBodyRenderModel.Place(entry, 0.25, latitude, 90.0, sun, 0.0);
        Assert.NotNull(sixHoursOn);
        Assert.Equal(atHome.AltitudeDeg, sixHoursOn.AltitudeDeg, 9);
    }

    /// <summary>
    /// The other half of the seam: a generated home moon, handed over as AstraTerra places it, actually
    /// works its way up and down its band over its own month instead of running one line every night.
    /// Authoring the track is no use if the numbers do not survive the handover -- an inclination that
    /// arrived as a declination would give a moon stuck at one height for the life of the world.
    /// </summary>
    [Fact]
    public void A_Home_Moon_Works_Its_Way_Up_And_Down_Its_Band_Over_A_Month()
    {
        var placement = PlanetWorldWithAMoon();
        var moon = NearSky.Author(placement).First(static body => body.Role == NearBodyRole.HomeMoon);
        var entry = Entry(moon);
        var month = 360.0 / moon.Track!.ArgumentRateDegPerDay;

        var declinations = new List<double>();
        for (var day = 0.0; day <= month; day += month / 200.0)
        {
            declinations.Add(NearBodyRenderModel.DeclinationDeg(entry, day));
        }

        // It reaches its inclination both ways and crosses the equator between, which is a band rather
        // than a line, and it is back where it started a month on.
        Assert.Equal(moon.Track.InclinationDeg, declinations.Max(), 1);
        Assert.Equal(-moon.Track.InclinationDeg, declinations.Min(), 1);
        Assert.Contains(declinations, static declination => Math.Abs(declination) < 1.0);
        Assert.Equal(
            NearBodyRenderModel.DeclinationDeg(entry, 0.0),
            NearBodyRenderModel.DeclinationDeg(entry, month),
            6);

        // And it keeps station with the stars rather than the ground: unlike the locked world's giant,
        // its right ascension is the same for every observer at a given moment.
        Assert.Equal(
            NearBodyRenderModel.RightAscensionDeg(entry, 3.0, localSiderealDeg: 0.0),
            NearBodyRenderModel.RightAscensionDeg(entry, 3.0, localSiderealDeg: 120.0, observerLongitudeDeg: 120.0),
            9);
    }

    private static GalaxyPlacement PlanetWorldWithAMoon()
    {
        for (var seed = 1L; seed < 400L; seed++)
        {
            var placement = GalaxyGenerator.Generate(seed);
            if (placement.WorldKind == ObserverWorldKind.TerrestrialPlanet
                && NearSky.Author(placement).Any(static body => body.Role == NearBodyRole.HomeMoon))
            {
                return placement;
            }
        }

        throw new InvalidOperationException("No planet world with a drawn moon was generated.");
    }

    private static GalaxyPlacement MoonWorld()
    {
        for (var seed = 1L; seed < 400L; seed++)
        {
            var placement = GalaxyGenerator.Generate(seed);
            if (placement.WorldKind == ObserverWorldKind.TerrestrialMoon
                && NearSky.Author(placement).Any(static body => body.Role == NearBodyRole.ParentGiant))
            {
                return placement;
            }
        }

        throw new InvalidOperationException("No moon world with a parent giant was generated.");
    }

    /// <summary>The record AstraTerra places, with a one-pixel stand-in for the painted face.</summary>
    private static NearBodyEntry Entry(NearBody body)
        => new(
            body.Id,
            body.DisplayName,
            NearBodyKind.ParentPlanet,
            body.AngularDiameterDeg,
            body.HourAngleDeg,
            body.HourAngleRateDegPerDay,
            body.DeclinationDeg,
            body.Brightness,
            new NearBodyFace(1, [unchecked((int)0xFFFFFFFF)], body.DiscFraction),
            Orbit: null,
            Track: body.Track is { } track ? NearSky.ToAstraTerraTrack(track) : null);
}
