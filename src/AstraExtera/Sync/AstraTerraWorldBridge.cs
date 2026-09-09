using AstraExtera.Config;
using AstraExtera.Galaxy;
using AstraTerra;
using Vintagestory.API.Common;

namespace AstraExtera.Sync;

/// <summary>
/// Hands AstraTerra the two things about a generated world that change the world rather than the
/// look of it: the giant that lights the ground, and the axis the world turns on.
/// </summary>
/// <remarks>
/// <para>
/// Both sides run this, which is the whole point of it existing separately from
/// <see cref="AstraTerraSkyBridge"/>. That one is a client: it paints faces and hands over a
/// drawing catalog, and a dedicated server neither can nor should do any of it. But what lights the
/// ground is what decides whether things spawn on it, and how far the world is tipped is what
/// decides how long its days are -- so a server that did not know either would be running different
/// rules from the world its players are looking at. AstraTerra holds one value per side and expects
/// each to be told; neither is authoritative, because both are doing the same arithmetic on the
/// same authored numbers.
/// </para>
/// <para>
/// Publishing again with the same sky is cheap and idempotent, so a reroll simply calls it again.
/// </para>
/// </remarks>
public sealed class AstraTerraWorldBridge
{
    private readonly ICoreAPI api;
    private readonly AstraExteraConfig config;

    public AstraTerraWorldBridge(ICoreAPI api, AstraExteraConfig config)
    {
        this.api = api ?? throw new ArgumentNullException(nameof(api));
        this.config = config ?? throw new ArgumentNullException(nameof(config));
    }

    /// <summary>
    /// Publishes this world's light source and tilt, or clears both for a world that has neither.
    /// </summary>
    /// <param name="placement">The stored placement this world was generated from.</param>
    /// <param name="bodies">
    /// The near sky already authored for it, when the caller has it. Authored here otherwise.
    /// </param>
    public void Publish(GalaxyPlacement placement, IReadOnlyList<NearBody>? bodies = null)
    {
        ArgumentNullException.ThrowIfNull(placement);
        var astraTerra = api.ModLoader.GetModSystem<AstraTerraModSystem>();
        if (astraTerra is null)
        {
            api.Logger.Warning(
                "AstraExtera found no AstraTerra mod system; this world's light source and tilt were not published.");
            return;
        }

        var lightSource = config.PublishNearBodyLight
            ? bodies is null
                ? NearBodyLightExport.BuildLightSource(placement)
                : NearBodyLightExport.BuildLightSource(placement, bodies)
            : null;
        var obliquityDeg = NearBodyLightExport.BuildWorldObliquityDeg(placement, config);

        astraTerra.SetNearBodyLightSource(lightSource);
        astraTerra.SetWorldObliquity(obliquityDeg);

        if (lightSource is null && obliquityDeg is null)
        {
            api.Logger.Event(
                "AstraExtera published no near-body light source and no world tilt: this world is lit and turned by Vintage Story's own rules.");
            return;
        }

        api.Logger.Event(
            "AstraExtera published the world's light: giant={0:0.0}deg wide at hourAngle={1:0.0}deg, declination={2:0.00}deg, albedo={3:0.00}; worldTilt={4}.",
            lightSource?.AngularDiameterDeg ?? 0.0,
            lightSource?.HourAngleDeg ?? 0.0,
            lightSource?.DeclinationDeg ?? 0.0,
            lightSource?.Albedo ?? 0.0,
            obliquityDeg is { } tilt ? $"{tilt:0.0}deg" : "Earth's");
    }
}
