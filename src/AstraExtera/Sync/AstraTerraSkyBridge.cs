using AstraExtera.Galaxy;
using AstraTerra;
using AstraTerra.Astronomy;
using Vintagestory.API.Client;

namespace AstraExtera.Sync;

/// <summary>
/// Hands this world's procedural star catalog to AstraTerra's sky renderer.
/// <para>
/// AstraTerra loads its shipped Earth catalog from an asset before a client knows which world it is
/// joining, so the swap can only happen once the server's galaxy placement has arrived. Earth's
/// constellation figures, guide groups and deep-sky objects are all keyed to Earth's own star ids
/// and sky positions, so none of them carry over; they are replaced with empty sets rather than
/// pointed at unrelated stars.
/// </para>
/// <para>
/// Earth's solar system goes the same way. Its planets have orbits authored around this Sun, its
/// comets are this system's, and its meteor showers are named for the constellations their radiants
/// sit in -- Perseids for Perseus, Leonids for Leo -- so under a sky with no inherited figures a
/// radiant is named for something nobody can point to. AstraExtera does not generate a planetary
/// system yet, so it says the sky has none rather than leaving the wrong one up.
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

    public void Publish(GalaxyPlacement placement)
    {
        if (publishedSeed == placement.WorldSeed)
        {
            return;
        }

        var astraTerra = api.ModLoader.GetModSystem<AstraTerraModSystem>();
        if (astraTerra is null)
        {
            api.Logger.Warning("AstraExtera found no AstraTerra mod system; the procedural sky was not published.");
            return;
        }

        var field = StarFieldSampler.Sample(placement);
        var catalog = new StarCatalog(
            StarCatalogExport.BuildEntries(placement, field)
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

        // Ordering matters: AstraTerra registers its creative bookshelf in AssetsFinalize, and the
        // Star Catalog, Zodiac and Planet Catalog books are all skipped when the catalog they would
        // describe is absent. Publishing on the placement packet lands before that.
        astraTerra.ReplacePlanetCatalog(null);
        astraTerra.ReplaceCometCatalog(null);
        astraTerra.ReplaceMeteorShowers([]);

        publishedSeed = placement.WorldSeed;
        api.Logger.Event(
            "AstraExtera published the procedural sky: stars={0}; nakedEyeStars={1:0}; limit=m{2:0.00}; effective=m{3:0.00}; pole={4:0.0}deg from the galactic pole; no planets, comets or meteor showers.",
            catalog.Stars.Count,
            field.ExpectedVisibleCount,
            field.LimitingMagnitude,
            field.EffectiveLimitingMagnitude,
            placement.Orientation.PoleTiltFromGalacticPoleDeg);
    }
}
