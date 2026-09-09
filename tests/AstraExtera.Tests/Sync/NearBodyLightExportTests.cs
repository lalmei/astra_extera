using AstraExtera.Config;
using AstraExtera.Galaxy;
using AstraExtera.Sync;
using AstraTerra.Astronomy;
using Xunit;

namespace AstraExtera.Tests.Sync;

/// <summary>
/// What a generated world hands AstraTerra about its own light: the giant that lights the ground,
/// and the axis the world turns on because it is locked to that giant.
/// </summary>
public sealed class NearBodyLightExportTests
{
    private const int SeedSweep = 400;

    /// <summary>
    /// The light source is the same giant the renderer draws, standing in the same place. Two
    /// placements would eventually disagree, and the disagreement would show up in a world as a
    /// full giant hanging over a dark landscape.
    /// </summary>
    [Fact]
    public void The_Giant_That_Lights_The_Ground_Is_The_Giant_On_The_Sky()
    {
        var placement = PlacementOfKind(ObserverWorldKind.TerrestrialMoon);
        var giant = NearSky.Author(placement).Single(static body => body.Role == NearBodyRole.ParentGiant);

        var source = NearBodyLightExport.BuildLightSource(placement);

        Assert.NotNull(source);
        Assert.Equal(giant.HourAngleDeg, source!.HourAngleDeg, 9);
        Assert.Equal(giant.DeclinationDeg, source.DeclinationDeg, 9);
        Assert.Equal(giant.Brightness, source.Albedo, 9);
    }

    /// <summary>
    /// What lights and eclipses is the globe, not the whole drawn face. Rings both reflect light and
    /// cast shadow in general, but a locked moon sits inside its giant's ring plane and sees them
    /// edge-on, where they do neither -- so a ringed giant's light source is strictly narrower than
    /// its picture, by the ring margin.
    /// </summary>
    [Fact]
    public void A_Ringed_Giant_Lights_By_Its_Globe_And_Not_Its_Rings()
    {
        var seen = 0;
        for (long seed = 1; seed <= SeedSweep; seed++)
        {
            var placement = GalaxyGenerator.Generate(seed);
            if (placement.WorldKind != ObserverWorldKind.TerrestrialMoon)
            {
                continue;
            }

            var bodies = NearSky.Author(placement);
            var giant = bodies.FirstOrDefault(static body => body.Role == NearBodyRole.ParentGiant);
            var source = NearBodyLightExport.BuildLightSource(placement, bodies);
            if (giant is null || source is null)
            {
                continue;
            }

            Assert.Equal(giant.AngularDiameterDeg * giant.DiscFraction, source.AngularDiameterDeg, 9);
            if (giant.DiscFraction < 1.0)
            {
                seen++;
                Assert.True(
                    source.AngularDiameterDeg < giant.AngularDiameterDeg,
                    $"seed {seed}: a ringed giant lit by its whole face over-eclipses by the ring margin");
            }
        }

        Assert.True(seen > 0, "no moon world in the sweep had a ringed giant");
    }

    /// <summary>
    /// A planet world has no parent giant and publishes nothing, so Vintage Story's own light is
    /// left alone. So does a server that has switched the hand-off off.
    /// </summary>
    [Fact]
    public void A_World_Without_A_Giant_Publishes_No_Light_Source()
    {
        Assert.Null(NearBodyLightExport.BuildLightSource(PlacementOfKind(ObserverWorldKind.TerrestrialPlanet)));
    }

    /// <summary>
    /// A moon world's tilt is its giant's, clamped to what the game's own weather can still be
    /// describing the same world as. The rings keep the giant's real tilt regardless -- the clamp is
    /// on what the moon inherits, not on what the giant is.
    /// </summary>
    [Fact]
    public void A_Moon_World_Turns_On_Its_Giants_Axis()
    {
        var config = new AstraExteraConfig();
        var seen = 0;
        var clamped = 0;

        for (long seed = 1; seed <= SeedSweep; seed++)
        {
            var placement = GalaxyGenerator.Generate(seed);
            if (placement.WorldKind != ObserverWorldKind.TerrestrialMoon
                || placement.System.ParentGiantAppearance is not { } appearance)
            {
                continue;
            }

            var obliquity = NearBodyLightExport.BuildWorldObliquityDeg(placement, config);

            seen++;
            Assert.NotNull(obliquity);
            Assert.InRange(obliquity!.Value, 0.0, config.GetMaxMoonWorldObliquityDeg());
            if (appearance.ObliquityDeg <= config.GetMaxMoonWorldObliquityDeg())
            {
                Assert.Equal(appearance.ObliquityDeg, obliquity.Value, 9);
            }
            else
            {
                clamped++;
                Assert.Equal(config.GetMaxMoonWorldObliquityDeg(), obliquity.Value, 9);
            }
        }

        Assert.True(seen > 0, "no moon world in the sweep had a parent giant to take a tilt from");
        Assert.True(clamped > 0, "no giant in the sweep was tipped past the cap, so the clamp is untested");
    }

    /// <summary>
    /// A planet world hands over nothing. Nothing generates a planet world's own obliquity, and
    /// inventing one to publish would be a different world rather than a better-described one.
    /// </summary>
    [Fact]
    public void A_Planet_World_Keeps_Earths_Tilt()
    {
        Assert.Null(
            NearBodyLightExport.BuildWorldObliquityDeg(
                PlacementOfKind(ObserverWorldKind.TerrestrialPlanet),
                new AstraExteraConfig()));
    }

    /// <summary>
    /// Both hand-offs are the server's to refuse, and refusing either leaves that half of the world
    /// to Vintage Story without disturbing the other.
    /// </summary>
    [Fact]
    public void A_Server_Can_Refuse_Either_Half()
    {
        var placement = PlacementOfKind(ObserverWorldKind.TerrestrialMoon);

        Assert.Null(
            NearBodyLightExport.BuildWorldObliquityDeg(
                placement,
                new AstraExteraConfig { PublishWorldObliquity = false }));

        // And a cap of Earth's own tilt is a world that behaves like the one the game was balanced
        // on, while still being tipped by its giant rather than by assumption.
        var earthCapped = NearBodyLightExport.BuildWorldObliquityDeg(
            placement,
            new AstraExteraConfig { MaxMoonWorldObliquityDeg = CelestialMath.MeanObliquityDeg });

        Assert.NotNull(earthCapped);
        Assert.InRange(earthCapped!.Value, 0.0, CelestialMath.MeanObliquityDeg);
    }

    /// <summary>
    /// The acceptance the whole change exists for: every locked moon gets a real eclipse season.
    /// The giant sits on the world's own celestial equator and the sun crosses its hour angle once
    /// every day, so whether that crossing is an eclipse depends only on where the sun's seasonal
    /// declination has got to -- and over a year it must reach the giant at least once, on every
    /// seed, or the model has authored a world where the phenomenon cannot happen.
    /// </summary>
    [Fact]
    public void Every_Moon_World_Gets_A_Real_Eclipse_Season()
    {
        const int daysPerYear = 360;
        var config = new AstraExteraConfig();
        var seen = 0;

        for (long seed = 1; seed <= SeedSweep; seed++)
        {
            var placement = GalaxyGenerator.Generate(seed);
            if (placement.WorldKind != ObserverWorldKind.TerrestrialMoon)
            {
                continue;
            }

            var source = NearBodyLightExport.BuildLightSource(placement);
            if (source is null)
            {
                continue;
            }

            seen++;
            var tiltDeg = NearBodyLightExport.BuildWorldObliquityDeg(placement, config)
                ?? CelestialMath.MeanObliquityDeg;
            var deepest = 0.0;
            for (var day = 0; day < daysPerYear && deepest < 0.999; day++)
            {
                for (var step = 0; step < 48 && deepest < 0.999; step++)
                {
                    deepest = Math.Max(
                        deepest,
                        Obscuration(source, day + (step / 48.0), daysPerYear, tiltDeg));
                }
            }

            Assert.True(
                deepest > 0.999,
                $"seed {seed}: a giant {source.AngularDiameterDeg:0.0} deg wide on a world tipped "
                    + $"{tiltDeg:0.0} deg never totally eclipsed its sun in a year (deepest {deepest:0.000})");
        }

        Assert.True(seen > 0, "no moon world in the sweep published a light source");
    }

    /// <summary>
    /// How much of the sun the giant covers at a moment, with the sun placed by this world's own
    /// tilt exactly as the game's solar delegate would place it, and the giant placed by the same
    /// arithmetic AstraTerra lights the ground with.
    /// </summary>
    private static double Obscuration(
        NearBodyLightSource source,
        double totalDays,
        int daysPerYear,
        double tiltDeg)
    {
        var (zenith, azimuth) = WorldTilt.SolarSphericalCoords(
            latitudeRad: 0.0,
            (totalDays % daysPerYear) / daysPerYear,
            totalDays % 1.0,
            WorldTilt.ToRadians(tiltDeg));
        var sinZenith = Math.Sin(zenith);
        var sun = new SkyDirection(
            sinZenith * Math.Sin(azimuth),
            Math.Cos(zenith),
            sinZenith * Math.Cos(azimuth));

        return NearBodyLightController
            .IlluminationFor(source, latitudeDeg: 0.0, longitudeDeg: 0.0, totalDays, daysPerYear, hoursPerDay: 24.0, sun)
            .SolarObscuration;
    }

    private static GalaxyPlacement PlacementOfKind(ObserverWorldKind kind)
    {
        for (long seed = 1; seed <= SeedSweep; seed++)
        {
            var placement = GalaxyGenerator.Generate(seed);
            if (placement.WorldKind == kind)
            {
                return placement;
            }
        }

        throw new InvalidOperationException($"No seed in {SeedSweep} produced a {kind}.");
    }
}
