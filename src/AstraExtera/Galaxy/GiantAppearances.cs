namespace AstraExtera.Galaxy;

/// <summary>
/// Gives a giant planet a face: the tilt it spins at, the bands that tilt whips up, a long-lived
/// storm parked between two of them, and -- often -- a ring system in its equatorial plane.
/// </summary>
/// <remarks>
/// <para>
/// None of this is derived from first principles the way the star and the habitable zone are. A
/// giant's banding, its spot, and whether it kept its rings are accidents of history that no
/// habitability argument settles, so they are sampled from ranges the solar system's four giants
/// bracket: rotation between eight and twenty hours, obliquity anywhere from Jupiter's three
/// degrees to Uranus lying on its side, and rings that are common but rarely as bright as Saturn's.
/// </para>
/// <para>
/// The one place this feeds back into physics is brightness. Ice rings are the most reflective
/// surfaces in a system, so an open, icy ring measurably lifts the planet's magnitude -- which is
/// how a ringed giant reaches the sky AstraTerra draws, where every planet is a point of light.
/// See <see cref="RingBrightnessBoostMagnitudes"/>.
/// </para>
/// </remarks>
public static class GiantAppearances
{
    /// <summary>Rings sitting inside this many planet radii are ground back to dust and lost.</summary>
    public const double MinRingInnerPlanetRadii = 1.10;

    /// <summary>Beyond roughly this, ring particles clump into moonlets instead of staying rings.</summary>
    public const double MaxRingOuterPlanetRadii = 4.20;

    public static GiantAppearance Sample(ref SplitMix64 rng, CompanionRole role, double massEarth)
    {
        var obliquity = SampleObliquity(ref rng);
        var (light, dark) = SampleBandColors(ref rng, role);
        var rotationHours = role == CompanionRole.OuterIceGiant
            ? rng.NextRange(14.0, 20.0)
            : rng.NextRange(8.0, 13.0);

        // Faster spinners drive more jets, and so more bands, which is what sets Jupiter apart from
        // the slower ice giants.
        var bandCount = (int)Math.Round(Math.Clamp(150.0 / rotationHours, 4.0, 16.0));
        bandCount += rng.NextInt(-1, 2);
        bandCount = Math.Clamp(bandCount, 3, 17);

        return new GiantAppearance(
            obliquity,
            Retrograde: rng.NextBool(0.12),
            rotationHours,
            AscendingNodeDeg: rng.NextRange(0.0, 360.0),
            bandCount,
            light.R,
            light.G,
            light.B,
            dark.R,
            dark.G,
            dark.B,
            SampleStorm(ref rng, role, bandCount, light, dark),
            SampleRing(ref rng, role, massEarth));
    }

    /// <summary>
    /// How wide open the rings look from the observer's own orbital plane. A ring lying in the
    /// orbital plane is seen edge-on and vanishes; a tipped one opens toward one node and closes at
    /// the other, so this is the average opening over an orbit rather than a single moment.
    /// </summary>
    public static double RingOpenness(GiantAppearance appearance)
    {
        ArgumentNullException.ThrowIfNull(appearance);
        var tilt = appearance.ObliquityDeg * Math.PI / 180.0;
        return Math.Clamp(Math.Abs(Math.Sin(tilt)) * 0.637, 0.0, 1.0);
    }

    /// <summary>
    /// Magnitudes a ring system adds to its planet, as a negative number. Saturn's rings roughly
    /// double its light when wide open, which is about 0.7 magnitudes; an edge-on or sooty ring adds
    /// nothing.
    /// </summary>
    public static double RingBrightnessBoostMagnitudes(GiantAppearance? appearance)
    {
        if (appearance?.Ring is not { } ring)
        {
            return 0.0;
        }

        var albedo = ring.Composition switch
        {
            RingComposition.Ice => 0.60,
            RingComposition.RockAndDust => 0.20,
            RingComposition.Soot => 0.06,
            _ => 0.20
        };

        // Ring light against planet light: the projected ring area, dimmed by how much of it the
        // particles actually fill and by how reflective they are, over the planet's own disc.
        var area = (ring.OuterRadiusPlanetRadii * ring.OuterRadiusPlanetRadii)
                   - (ring.InnerRadiusPlanetRadii * ring.InnerRadiusPlanetRadii);
        var share = area * ring.OpticalDepth * albedo * RingOpenness(appearance);
        return -2.5 * Math.Log10(1.0 + Math.Max(0.0, share));
    }

    /// <summary>Which way the ring line runs on the page, in radians, for the system figures.</summary>
    public static double RingRollRadians(GiantAppearance appearance)
    {
        ArgumentNullException.ThrowIfNull(appearance);
        var roll = ((appearance.AscendingNodeDeg % 180.0) - 90.0) * Math.PI / 180.0;
        return appearance.Retrograde ? -roll : roll;
    }

    private static double SampleObliquity(ref SplitMix64 rng)
    {
        var roll = rng.NextUnit();
        return roll switch
        {
            // Most giants stand nearly upright, like Jupiter.
            < 0.45 => rng.NextRange(0.5, 12.0),

            // A giant knocked over in the last stages of accretion, like Saturn or Neptune.
            < 0.85 => rng.NextRange(15.0, 40.0),

            // And the rare world lying on its side, like Uranus.
            _ => rng.NextRange(60.0, 98.0)
        };
    }

    private static ((float R, float G, float B) Light, (float R, float G, float B) Dark) SampleBandColors(
        ref SplitMix64 rng,
        CompanionRole role)
    {
        if (role == CompanionRole.OuterIceGiant)
        {
            // Methane over hydrogen: the colder the haze, the further from teal toward deep blue.
            var light = (
                (float)rng.NextRange(0.55, 0.74),
                (float)rng.NextRange(0.82, 0.94),
                (float)rng.NextRange(0.93, 1.00));
            var dark = (
                (float)rng.NextRange(0.10, 0.24),
                (float)rng.NextRange(0.34, 0.52),
                (float)rng.NextRange(0.62, 0.82));
            return (light, dark);
        }

        // Ammonia cloud decks over sulfur and phosphorus hazes: cream zones and ruddy belts.
        var warm = rng.NextUnit();
        var lightBand = (
            (float)rng.NextRange(0.90, 1.00),
            (float)rng.NextRange(0.84, 0.95),
            (float)rng.NextRange(0.66 + (0.14 * (1.0 - warm)), 0.86));
        var darkBand = (
            (float)rng.NextRange(0.52, 0.74),
            (float)rng.NextRange(0.30, 0.48),
            (float)rng.NextRange(0.16, 0.32));
        return (lightBand, darkBand);
    }

    private static PlanetStorm? SampleStorm(
        ref SplitMix64 rng,
        CompanionRole role,
        int bandCount,
        (float R, float G, float B) light,
        (float R, float G, float B) dark)
    {
        var chance = role == CompanionRole.OuterIceGiant ? 0.45 : 0.80;
        if (!rng.NextBool(chance))
        {
            return null;
        }

        // Anticyclones are pinned between two jets, so a storm sits on a band boundary rather than
        // anywhere on the globe.
        var band = rng.NextInt(1, Math.Max(2, bandCount - 1));
        var latitude = ((band / (double)bandCount) - 0.5) * 2.0 * 62.0;
        if (rng.NextBool(0.5))
        {
            latitude = -latitude;
        }

        var span = rng.NextRange(18.0, 46.0);
        var (r, g, b) = role == CompanionRole.OuterIceGiant
            ? ((float)rng.NextRange(0.08, 0.20), (float)rng.NextRange(0.16, 0.30), (float)rng.NextRange(0.40, 0.62))
            : ((float)rng.NextRange(0.72, 0.95), (float)rng.NextRange(0.26, 0.52), (float)rng.NextRange(0.18, 0.36));

        // A pale storm on a dark band reads as well as a dark one on a pale band; pick whichever
        // contrasts with the deck it sits on.
        if (rng.NextBool(0.25))
        {
            (r, g, b) = (
                (float)Math.Clamp((light.R + dark.R) * 0.5 + 0.18, 0.0, 1.0),
                (float)Math.Clamp((light.G + dark.G) * 0.5 + 0.14, 0.0, 1.0),
                (float)Math.Clamp((light.B + dark.B) * 0.5 + 0.10, 0.0, 1.0));
        }

        return new PlanetStorm(
            StormNames[rng.NextInt(StormNames.Length)],
            latitude,
            span,
            LatitudeSpanDeg: span * rng.NextRange(0.35, 0.60),
            AgeYears: rng.NextRange(80.0, 4200.0),
            r,
            g,
            b);
    }

    private static PlanetRing? SampleRing(ref SplitMix64 rng, CompanionRole role, double massEarth)
    {
        // Every giant in the solar system has rings; only one has rings worth seeing. Massive
        // giants hold theirs longest, so mass tips the odds rather than deciding them.
        var chance = role switch
        {
            CompanionRole.ShepherdGiant => 0.72,
            CompanionRole.OuterGasGiant => 0.68,
            CompanionRole.OuterIceGiant => 0.45,
            _ => 0.0
        };

        chance += Math.Clamp((massEarth - 100.0) / 1200.0, -0.08, 0.12);
        if (!rng.NextBool(chance))
        {
            return null;
        }

        var inner = rng.NextRange(MinRingInnerPlanetRadii, 1.85);
        var outer = inner + rng.NextRange(0.35, 2.30);
        outer = Math.Min(outer, MaxRingOuterPlanetRadii);
        if (outer <= inner + 0.15)
        {
            outer = inner + 0.15;
        }

        var composition = SampleComposition(ref rng, role);
        var opticalDepth = composition switch
        {
            RingComposition.Ice => rng.NextRange(0.35, 0.95),
            RingComposition.RockAndDust => rng.NextRange(0.08, 0.40),
            _ => rng.NextRange(0.03, 0.22)
        };

        // A moonlet in resonance sweeps one lane clear, the way Mimas keeps the Cassini division.
        var division = rng.NextBool(0.55)
            ? inner + ((outer - inner) * rng.NextRange(0.35, 0.75))
            : 0.0;

        var (r, g, b) = composition switch
        {
            RingComposition.Ice => (
                (float)rng.NextRange(0.86, 1.00),
                (float)rng.NextRange(0.88, 1.00),
                (float)rng.NextRange(0.90, 1.00)),
            RingComposition.RockAndDust => (
                (float)rng.NextRange(0.72, 0.90),
                (float)rng.NextRange(0.58, 0.74),
                (float)rng.NextRange(0.42, 0.58)),
            _ => (
                (float)rng.NextRange(0.34, 0.48),
                (float)rng.NextRange(0.30, 0.42),
                (float)rng.NextRange(0.30, 0.44))
        };

        return new PlanetRing(inner, outer, opticalDepth, division, composition, r, g, b);
    }

    private static RingComposition SampleComposition(ref SplitMix64 rng, CompanionRole role)
    {
        var roll = rng.NextUnit();
        if (role == CompanionRole.OuterIceGiant)
        {
            // Far from the star, ring ice darkens under irradiation rather than staying bright.
            return roll < 0.30 ? RingComposition.Ice
                : roll < 0.70 ? RingComposition.RockAndDust
                : RingComposition.Soot;
        }

        return roll < 0.58 ? RingComposition.Ice
            : roll < 0.90 ? RingComposition.RockAndDust
            : RingComposition.Soot;
    }

    private static readonly string[] StormNames =
    [
        "the Great Eye",
        "the Long Storm",
        "the Red Wake",
        "the Amber Spot",
        "the Standing Gyre",
        "the Pale Oval",
        "the Old Wound",
        "the Slow Whorl"
    ];
}
