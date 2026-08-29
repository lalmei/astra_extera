using AstraExtera.Galaxy;
using Xunit;

namespace AstraExtera.Tests.Galaxy;

public sealed class GiantAppearanceTests
{
    [Fact]
    public void Every_Giant_Has_A_Face_And_Every_Rocky_Companion_Has_None()
    {
        var ringed = 0;
        var stormy = 0;
        var moons = 0;
        var compositions = new HashSet<RingComposition>();

        for (long seed = 1; seed <= 256; seed++)
        {
            var system = GalaxyGenerator.Generate(seed).System;

            foreach (var body in system.Companions)
            {
                if (!body.IsGiant)
                {
                    Assert.Null(body.Appearance);
                    Assert.Empty(body.Moons);
                    continue;
                }

                var appearance = Assert.IsType<GiantAppearance>(body.Appearance);
                Assert.InRange(appearance.ObliquityDeg, 0.0, 98.0);
                Assert.InRange(appearance.BandCount, 3, 17);
                Assert.InRange(appearance.RotationPeriodHours, 8.0, 20.0);
                Assert.InRange(appearance.AscendingNodeDeg, 0.0, 360.0);

                if (appearance.Storm is { } storm)
                {
                    stormy++;
                    Assert.InRange(storm.LatitudeDeg, -70.0, 70.0);
                    Assert.True(storm.LongitudeSpanDeg > 0.0);
                    Assert.False(string.IsNullOrWhiteSpace(storm.Name));
                }

                if (appearance.Ring is { } ring)
                {
                    ringed++;
                    compositions.Add(ring.Composition);
                    Assert.InRange(
                        ring.InnerRadiusPlanetRadii,
                        GiantAppearances.MinRingInnerPlanetRadii,
                        GiantAppearances.MaxRingOuterPlanetRadii);
                    Assert.True(ring.OuterRadiusPlanetRadii > ring.InnerRadiusPlanetRadii);
                    Assert.InRange(ring.OuterRadiusPlanetRadii, 0.0, GiantAppearances.MaxRingOuterPlanetRadii);
                    Assert.InRange(ring.OpticalDepth, 0.0, 1.0);
                }

                moons += body.Moons.Length;
                foreach (var moon in body.Moons)
                {
                    Assert.False(moon.Habitable);
                    Assert.True(moon.RadiusEarth > 0.0);
                    Assert.True(moon.DayLengthDays > 0.0);
                    Assert.InRange(moon.DayLengthDays, 0.0, LocalSystem.MaxGiantMoonMonthDays * 1.2);
                    Assert.False(string.IsNullOrWhiteSpace(moon.DisplayName));
                }
            }
        }

        Assert.True(ringed > 0, "no giant in 256 systems kept a ring");
        Assert.True(stormy > 0, "no giant in 256 systems held a storm");
        Assert.True(moons > 0, "no giant in 256 systems held a moon");
        Assert.True(compositions.Count > 1, "every ring in 256 systems was made of the same stuff");
    }

    /// <summary>
    /// Rings only reach AstraTerra's sky as brightness, because it draws every planet as a point of
    /// light. An open sheet of ice has to make its planet brighter, and an edge-on one cannot.
    /// </summary>
    [Fact]
    public void Open_Ice_Rings_Brighten_Their_Planet_And_Edge_On_Rings_Do_Not()
    {
        var ring = new PlanetRing(1.2, 3.0, 0.8, 0.0, RingComposition.Ice, 0.95f, 0.95f, 0.95f);
        var open = Face(60.0, ring);
        var edgeOn = Face(0.0, ring);

        Assert.True(GiantAppearances.RingBrightnessBoostMagnitudes(open) < -0.5);
        Assert.Equal(0.0, GiantAppearances.RingBrightnessBoostMagnitudes(edgeOn), 3);
        Assert.Equal(0.0, GiantAppearances.RingBrightnessBoostMagnitudes(Face(60.0, ring: null)), 3);
        Assert.Equal(0.0, GiantAppearances.RingBrightnessBoostMagnitudes(null), 3);

        Assert.InRange(GiantAppearances.RingOpenness(edgeOn), 0.0, 0.01);
        Assert.True(GiantAppearances.RingOpenness(open) > 0.4);
    }

    [Fact]
    public void A_Ringed_Giant_Outshines_The_Same_Giant_Without_Rings()
    {
        var sky = LocalSystemSky.Author(RingedPlacement(out var ringedIndex));
        var planet = sky.Planets[ringedIndex];

        Assert.True(planet.AbsoluteMagnitude < -6.0);
        Assert.InRange(planet.TintR, 0.0f, 1.0f);
        Assert.InRange(planet.TintG, 0.0f, 1.0f);
        Assert.InRange(planet.TintB, 0.0f, 1.0f);
    }

    /// <summary>
    /// The portrait strip is drawn into a fixed box in the panel and on the preview page, so nothing
    /// -- disc, ring, moon row or label -- may leave it, however wide a ring or however it is rolled.
    /// </summary>
    [Fact]
    public void Portraits_Stay_Inside_Their_Own_Box()
    {
        for (long seed = 1; seed <= 120; seed++)
        {
            var system = GalaxyGenerator.Generate(seed).System;
            var portraits = PlanetPortraits.Layout(system);
            Assert.Equal(system.Companions.Length, portraits.Count);

            var slotWidth = PlanetPortraits.ViewWidth / Math.Max(1, portraits.Count);
            foreach (var portrait in portraits)
            {
                var rise = PlanetPortraits.RingRise(portrait);
                var top = portrait.Cy - portrait.DiscPx - rise - PlanetPortraits.MoonReservePx;
                var bottom = portrait.Cy + portrait.DiscPx + rise + PlanetPortraits.LabelReservePx;

                Assert.True(top >= -0.5, $"seed {seed}: a portrait ran off the top at {top:0.0}");
                Assert.True(
                    bottom <= PlanetPortraits.ViewHeight + 0.5,
                    $"seed {seed}: a portrait ran off the bottom at {bottom:0.0}");

                var halfWidth = portrait.HasRing ? portrait.RingOuterPx : portrait.DiscPx;
                Assert.True(
                    halfWidth <= (slotWidth * 0.5) + 0.5,
                    $"seed {seed}: a portrait ran into its neighbour by {halfWidth - (slotWidth * 0.5):0.0}");
            }
        }
    }

    [Fact]
    public void A_Stored_Giant_Keeps_Its_Rings_And_Its_Moons_Across_The_Save()
    {
        var original = RingedPlacement(out _);
        var restored = GalaxyPlacementCodec.FromUtf8(GalaxyPlacementCodec.ToUtf8(original));

        Assert.Equal(original, restored);
        var giant = original.System.Companions.First(static body => body.Ring is not null);
        var stored = restored.System.Companions.First(static body => body.Ring is not null);
        Assert.Equal(giant.Ring, stored.Ring);
        Assert.Equal(giant.Appearance, stored.Appearance);
        Assert.Equal(giant.Moons, stored.Moons);
    }

    private static GiantAppearance Face(double obliquityDeg, PlanetRing? ring)
        => new(
            obliquityDeg,
            Retrograde: false,
            RotationPeriodHours: 10.0,
            AscendingNodeDeg: 40.0,
            BandCount: 9,
            0.95f,
            0.90f,
            0.75f,
            0.60f,
            0.40f,
            0.25f,
            Storm: null,
            ring);

    private static GalaxyPlacement RingedPlacement(out int planetIndex)
    {
        for (long seed = 1; seed <= 400; seed++)
        {
            var placement = GalaxyGenerator.Generate(seed);
            var index = Array.FindIndex(placement.System.Companions, static body => body.Ring is not null);
            if (index >= 0)
            {
                planetIndex = index;
                return placement;
            }
        }

        throw new InvalidOperationException("No seed in 400 authored a ringed giant.");
    }
}
