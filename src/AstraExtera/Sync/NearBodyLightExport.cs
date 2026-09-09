using AstraExtera.Config;
using AstraExtera.Galaxy;
using AstraTerra.Astronomy;

namespace AstraExtera.Sync;

/// <summary>
/// The half of a moon world's parent giant that changes the world rather than the view of it: the
/// light it puts on the ground at night, the sun it takes away during an eclipse, and the axis the
/// world turns on because it is locked to that giant.
/// </summary>
/// <remarks>
/// <para>
/// Separate from <see cref="NearBodyExport"/>, and separate for a reason that matters. That one
/// builds a catalog with a painted face per body, which a dedicated server has no way to draw and
/// no business decoding; this is a handful of numbers, and the server needs every one of them,
/// because the light a server computes is what decides whether things spawn in the open at night.
/// So the catalog goes to the client and this goes to both sides.
/// </para>
/// <para>
/// Both values are authored somewhere else and only selected here. The giant's placement comes from
/// <see cref="NearSky.Author"/>, so the giant that lights the ground is the same object the player
/// can see hanging there -- two placements would eventually disagree, and the disagreement would
/// look like a full giant over a dark landscape. The tilt comes from the giant's own appearance,
/// which the generator was already drawing to tilt the rings.
/// </para>
/// </remarks>
public static class NearBodyLightExport
{
    /// <summary>
    /// The giant that lights this world, or null for a world that has none.
    /// </summary>
    /// <remarks>
    /// Null on a planet world, and on a moon world whose system came out without a parent giant --
    /// both of which are worlds Vintage Story should light on its own, untouched.
    /// </remarks>
    public static NearBodyLightSource? BuildLightSource(GalaxyPlacement placement)
    {
        ArgumentNullException.ThrowIfNull(placement);
        return BuildLightSource(placement, NearSky.Author(placement));
    }

    /// <param name="bodies">
    /// The near sky already authored for this placement. <see cref="NearSky.Author"/> is
    /// deterministic from the seed, so authoring it twice is correct rather than merely harmless --
    /// but a caller that has it in hand should pass it, because these two exports run side by side.
    /// </param>
    public static NearBodyLightSource? BuildLightSource(
        GalaxyPlacement placement,
        IReadOnlyList<NearBody> bodies)
    {
        ArgumentNullException.ThrowIfNull(placement);
        ArgumentNullException.ThrowIfNull(bodies);
        if (placement.WorldKind != ObserverWorldKind.TerrestrialMoon)
        {
            return null;
        }

        var giant = bodies.FirstOrDefault(static body => body.Role == NearBodyRole.ParentGiant);
        if (giant is null)
        {
            return null;
        }

        return new NearBodyLightSource(
            // The globe, not the drawn face. A NearBody's angular diameter is the whole thing
            // including its rings, and DiscFraction is the globe's share of it. Rings reflect light
            // and cast shadow, but a locked moon sits inside its giant's ring plane and sees them
            // edge-on, where they do neither in any amount worth modelling -- so passing the face
            // through would over-light and over-eclipse by the ring margin, which on a heavily
            // ringed giant is more than a factor of two.
            AngularDiameterDeg: giant.AngularDiameterDeg * giant.DiscFraction,
            giant.HourAngleDeg,
            giant.DeclinationDeg,

            // What the face is drawn at doubles as a bond albedo. They are not the same quantity --
            // one is how bright to paint a disc, the other is how much light comes back off a
            // sphere -- but a generated giant's authored brightness lands close enough to a real
            // giant's albedo that a second number would be false precision.
            Albedo: giant.Brightness);
    }

    /// <summary>
    /// The axis this world turns on, in degrees, or null for a world that keeps Earth's.
    /// </summary>
    /// <remarks>
    /// <para>
    /// A tidally locked moon has no tilt of its own to generate. It formed in the disc that became
    /// its giant's rings, it orbits in that giant's equatorial plane, and it is locked to it -- so
    /// its spin axis is the giant's spin axis, and the giant's obliquity is the moon's. That number
    /// decides how strong this world's seasons are and, with the giant sitting on the moon's own
    /// equator, how often the giant walks across the sun.
    /// </para>
    /// <para>
    /// A planet world gets null. Nothing generates a planet world's own obliquity today, and
    /// inventing one to hand over would be a different world rather than a better-described one.
    /// </para>
    /// <para>
    /// The clamp is the honest part. Vintage Story's seasonal temperature curve follows its
    /// calendar rather than its sun, so past about forty degrees a world's daylight and its weather
    /// stop describing the same place. The giant keeps its full tilt for its rings; what the moon
    /// inherits is capped, and the cap is a setting.
    /// </para>
    /// </remarks>
    public static double? BuildWorldObliquityDeg(GalaxyPlacement placement, AstraExteraConfig config)
    {
        ArgumentNullException.ThrowIfNull(placement);
        ArgumentNullException.ThrowIfNull(config);
        if (!config.PublishWorldObliquity
            || placement.WorldKind != ObserverWorldKind.TerrestrialMoon
            || placement.System.ParentGiantAppearance is not { } appearance
            || !double.IsFinite(appearance.ObliquityDeg))
        {
            return null;
        }

        return Math.Clamp(
            appearance.ObliquityDeg,
            0.0,
            config.GetMaxMoonWorldObliquityDeg());
    }
}
