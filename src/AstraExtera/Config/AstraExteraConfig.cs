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
}
