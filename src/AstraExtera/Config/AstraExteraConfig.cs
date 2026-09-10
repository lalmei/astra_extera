using AstraExtera.Galaxy;

namespace AstraExtera.Config;

/// <summary>
/// The settings a server keeps for the world AstraExtera generated, as opposed to the ones
/// AstraTerra keeps for how that world is drawn.
/// </summary>
/// <remarks>
/// Everything here is about how much of a generated world reaches the game rules rather than the
/// view. The sky is generated from the world seed and is not configurable -- rerolling is what
/// changes a sky -- but whether a moon world's parent giant lights the ground, and how far the
/// world is allowed to be tipped, are decisions about the world a server is asking people to live
/// on. AstraTerra has the switches for the model itself; these decide what it is handed.
/// </remarks>
public sealed class AstraExteraConfig
{
    /// <summary>
    /// The furthest a generated moon world will be tipped, in degrees, however far its giant is.
    /// </summary>
    /// <remarks>
    /// <para>
    /// A locked moon's axis is its giant's, and giants are generated up to 98 degrees -- Uranus
    /// lying on its side. Handed over unclamped that is a world where the sun spends half the year
    /// circling one pole and never rises at the other, which is a real place and a striking sky.
    /// It is also a world Vintage Story's own seasonal temperature curve knows nothing about: the
    /// curve follows the calendar's seasons rather than the sun, so a world tipped past about forty
    /// degrees has months of polar night at a temperature that thinks it is autumn.
    /// </para>
    /// <para>
    /// Forty-five is the far edge of what stays coherent: seasons well past Earth's, long summer
    /// days and short winter ones at a mid latitude, and no polar night anywhere a player is likely
    /// to settle. Raise it for the sky and accept that the weather has not been told; lower it, or
    /// set it to Earth's 23.44, for a world that behaves like the one the game was balanced on. The
    /// rings are drawn from the giant's real tilt either way -- this clamps what the moon inherits,
    /// not what the giant is.
    /// </para>
    /// </remarks>
    public double MaxMoonWorldObliquityDeg { get; set; } = DefaultMaxMoonWorldObliquityDeg;

    /// <summary>
    /// Whether a moon world's tilt is handed to AstraTerra at all. Off leaves every world on
    /// Earth's 23.4 degrees, and every generated moon reading the same as every other.
    /// </summary>
    public bool PublishWorldObliquity { get; set; } = true;

    /// <summary>
    /// Whether the parent giant is handed over as a light source -- the planetshine on a locked
    /// moon's night, and the eclipses when it crosses the sun.
    /// </summary>
    /// <remarks>
    /// AstraTerra has its own <c>NearBodyLighting</c> switch for the model; this one decides
    /// whether it is fed. Both default to on, and either off is enough to leave the world's light
    /// to Vintage Story. The giant is still drawn either way: this is about the ground, not the
    /// sky.
    /// </remarks>
    public bool PublishNearBodyLight { get; set; } = true;

    /// <summary>
    /// Which kind of world to generate: <c>any</c>, <c>planet</c>, or <c>moon</c>.
    /// </summary>
    /// <remarks>
    /// Everything from here down is a constraint on the generator rather than a value handed to it.
    /// The pipeline is a rejection sampler that checks the habitable zone, the Roche limit, Hill
    /// separation, the star's lifespan and the shortest year a planet can have without locking to
    /// its star; a setting that narrowed what it draws and let it keep drawing preserves every one
    /// of those, where a setting that wrote a value into the result would let a server author a
    /// world that cannot exist. Each of these defaults to <c>any</c>, which is the generator's own
    /// behaviour: the same seed gives the same sky it gave before any of this was configurable.
    /// <para>
    /// These apply when a save's sky is first authored and when an admin runs
    /// <c>/astraextera reroll</c>. They do not touch a save that already has a stored placement --
    /// changing the config cannot silently invalidate a sky people have already named stars in.
    /// </para>
    /// </remarks>
    public string WorldKind { get; set; } = AnyValue;

    /// <summary>
    /// The host star's spectral class: <c>any</c>, <c>M</c>, <c>K</c>, <c>G</c>, or <c>F</c>.
    /// </summary>
    /// <remarks>
    /// <c>M</c> implies a moon world. An M dwarf's habitable zone is close enough in that a planet
    /// there would tidally lock, which would freeze Vintage Story's day and night; a moon keeps its
    /// day from circling its giant instead, so it is the only kind of world that can live under one.
    /// </remarks>
    public string StarClass { get; set; } = AnyValue;

    /// <summary>
    /// The shape of the host galaxy: <c>any</c>, <c>spiral</c>, or <c>elliptical</c>.
    /// </summary>
    /// <remarks>
    /// Ellipticals are generated at <see cref="GalaxyGenerator.EllipticalProbability"/>, about one
    /// world in forty, and they are the reason to set this: a starless, dust-free sky with a
    /// spheroid instead of a band across it is not something a server owner will reach by rerolling.
    /// </remarks>
    public string GalaxyMorphology { get; set; } = AnyValue;

    /// <summary>
    /// Whether the giant that dominates the world's sky has rings: <c>any</c>, <c>required</c>, or
    /// <c>none</c>.
    /// </summary>
    /// <remarks>
    /// That giant is the parent a moon world orbits, and for a planet world the shepherd giant past
    /// the snow line -- the one it actually sees. Rings are not derived from habitability, so this
    /// forces the draw rather than rejecting it, and how open, how bright and what colour the ring
    /// is are sampled as they always were.
    /// </remarks>
    public string ParentGiantRings { get; set; } = AnyValue;

    /// <summary>
    /// Whether a planet world has moons of its own: <c>any</c>, <c>required</c>, or <c>none</c>.
    /// </summary>
    /// <remarks>
    /// Planet worlds only. A moon world's family belongs to its giant and it is a member of that
    /// family rather than a host of one, so asking a moon world for moons is a contradiction; the
    /// loader says so and ignores this setting rather than generating nothing.
    /// </remarks>
    public string HomeMoons { get; set; } = AnyValue;

    public const string AnyValue = "any";

    public const double DefaultMaxMoonWorldObliquityDeg = 45.0;

    /// <summary>The most a world may be tipped, past which AstraTerra would clamp it anyway.</summary>
    public const double MaxObliquityDeg = 90.0;

    /// <summary>
    /// The configured cap, brought back into a range that means something. A value below zero or
    /// above a right angle is not a world, and a missing one is the default.
    /// </summary>
    public double GetMaxMoonWorldObliquityDeg()
        => double.IsFinite(MaxMoonWorldObliquityDeg)
            ? Math.Clamp(MaxMoonWorldObliquityDeg, 0.0, MaxObliquityDeg)
            : DefaultMaxMoonWorldObliquityDeg;

    /// <summary>
    /// The configured settings as the generator's own vocabulary, with anything unreadable widened
    /// back to "any" and named in <paramref name="rejected"/> for the loader to warn about. A
    /// typo must not quietly become a constraint nobody asked for, nor stop the world loading.
    /// </summary>
    public GalaxyConstraints GetGalaxyConstraints(out IReadOnlyList<string> rejected)
    {
        var complaints = new List<string>(5);
        var constraints = new GalaxyConstraints(
            Parse(WorldKind, nameof(WorldKind), WorldKindConstraint.Any, complaints),
            Parse(StarClass, nameof(StarClass), StarClassConstraint.Any, complaints),
            Parse(GalaxyMorphology, nameof(GalaxyMorphology), MorphologyConstraint.Any, complaints),
            Parse(ParentGiantRings, nameof(ParentGiantRings), PresenceConstraint.Any, complaints),
            Parse(HomeMoons, nameof(HomeMoons), PresenceConstraint.Any, complaints));

        rejected = complaints;
        return constraints;
    }

    public GalaxyConstraints GetGalaxyConstraints() => GetGalaxyConstraints(out _);

    private static T Parse<T>(string? value, string setting, T fallback, List<string> complaints)
        where T : struct, Enum
    {
        if (string.IsNullOrWhiteSpace(value) || value.Trim().Equals(AnyValue, StringComparison.OrdinalIgnoreCase))
        {
            return fallback;
        }

        if (Enum.TryParse<T>(value.Trim(), ignoreCase: true, out var parsed) && Enum.IsDefined(parsed))
        {
            return parsed;
        }

        var allowed = string.Join(
            ", ",
            Enum.GetNames<T>().Select(name => name.Equals("Any", StringComparison.Ordinal) ? AnyValue : name.ToLowerInvariant()));
        complaints.Add($"{setting} '{value}' is not one of {allowed}; using '{AnyValue}'.");
        return fallback;
    }
}
