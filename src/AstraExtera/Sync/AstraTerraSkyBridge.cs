using AstraExtera.Galaxy;
using AstraTerra;
using AstraTerra.Astronomy;
using Vintagestory.API.Client;

namespace AstraExtera.Sync;

/// <summary>
/// Hands this world's stored sky to AstraTerra: stars, companion planets, comets, and the meteor
/// showers those comets leave on the observer's orbit.
/// <para>
/// AstraTerra loads its shipped Earth catalogs from assets before a client knows which world it is
/// joining, so the swap can only happen once the server's galaxy packet has arrived. Earth's
/// constellation figures, guide groups and deep-sky objects are all keyed to Earth's own star ids
/// and sky positions, so none of them carry over; they are replaced with empty sets rather than
/// pointed at unrelated stars. Earth's planets, comets and showers are replaced the same way.
/// </para>
/// </summary>
public sealed class AstraTerraSkyBridge
{
    private readonly ICoreClientAPI api;
    private long? publishedSeed;

    public AstraTerraSkyBridge(ICoreClientAPI api)
    {
        this.api = api;
    }

    public void Publish(GalaxySky sky)
    {
        ArgumentNullException.ThrowIfNull(sky);
        if (publishedSeed == sky.Placement.WorldSeed)
        {
            return;
        }

        var astraTerra = api.ModLoader.GetModSystem<AstraTerraModSystem>();
        if (astraTerra is null)
        {
            api.Logger.Warning("AstraExtera found no AstraTerra mod system; the procedural sky was not published.");
            return;
        }

        var catalog = new StarCatalog(
            StarCatalogExport.BuildEntries(sky.Placement, sky.StarField)
                .Select(entry => new StarCatalogEntry(
                    entry.Hip,
                    entry.RightAscensionDeg,
                    entry.DeclinationDeg,
                    entry.VisualMagnitude,
                    entry.BvColorIndex,
                    entry.IsGuideStar))
                .ToList(),
            guideGroups: [],
            skyCultures: [],
            deepSkyObjects: []);

        if (!astraTerra.ReplaceStarCatalog(catalog))
        {
            api.Logger.Warning("AstraExtera could not publish the procedural sky: AstraTerra astronomy is disabled.");
            return;
        }

        astraTerra.ReplacePlanetCatalog(LocalSystemSkyExport.ToPlanetCatalog(sky.LocalSky));
        astraTerra.ReplaceCometCatalog(LocalSystemSkyExport.ToCometCatalog(sky.LocalSky));
        astraTerra.ReplaceMeteorShowers(LocalSystemSkyExport.ToMeteorShowers(sky.LocalSky));

        // A world that is itself a moon does not get Earth's moon: it gets the giant it orbits,
        // fixed in one spot because it is tidally locked to it, and its sibling moons.
        var nearBodies = NearBodyExport.Build(sky.Placement);
        astraTerra.ReplaceNearBodies(nearBodies);

        publishedSeed = sky.Placement.WorldSeed;
        api.Logger.Event(
            "AstraExtera published the stored sky: stars={0}; nakedEyeStars={1:0}; planets={2}; comets={3}; showers={4}; nearBodies={9} (vanilla moon hidden={10}); pole={5:0.0}deg from the galactic pole; host={6} {7:0.00} Msun at {8:0.00} AU.",
            catalog.Stars.Count,
            sky.StarField.ExpectedVisibleCount,
            sky.LocalSky.Planets.Count,
            sky.LocalSky.Comets.Count,
            sky.LocalSky.Showers.Count,
            sky.Placement.Orientation.PoleTiltFromGalacticPoleDeg,
            sky.Placement.System.StarClassLabel,
            sky.Placement.System.StarMassSolar,
            sky.Placement.System.OrbitalDistanceAu,
            nearBodies.Bodies.Count,
            nearBodies.HidesVanillaMoon);
    }
}
