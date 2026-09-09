using AstraExtera.Galaxy;
using AstraTerra;
using AstraTerra.Observation;
using Vintagestory.API.Common;

namespace AstraExtera.Sync;

/// <summary>
/// Hands AstraTerra the catalogs that say what is in this world's sky: its stars, the companion
/// planets, the authored comet apparitions, and the showers those comets throw.
/// </summary>
/// <remarks>
/// <para>
/// Both sides run this, which is the whole point of it existing separately from
/// <see cref="AstraTerraSkyBridge"/>. That one paints faces and hands over a drawing catalog, which
/// a dedicated server neither can nor should do. These four are not only drawn: AstraTerra reads its
/// star catalog on the server as well, to check a constellation a player submits, to fill a prepared
/// book, and to answer <c>/stars</c>. A server left holding Earth's shipped catalog would be
/// validating figures against a sky none of its players can see, and resolving star ids to the wrong
/// stars. Nothing here needs a client: the server stores the star field and the local sky itself, and
/// none of it is a picture.
/// </para>
/// <para>
/// AstraTerra loads Earth's catalogs from assets before either side knows which world is being
/// joined, so the swap can only happen once this save's sky is known -- on the server when the save
/// loads, and on a client when the server's packet arrives.
/// </para>
/// <para>
/// Publishing again with the same sky is cheap and idempotent, so a reroll simply calls it again.
/// </para>
/// </remarks>
public sealed class AstraTerraCatalogBridge
{
    private readonly ICoreAPI api;

    public AstraTerraCatalogBridge(ICoreAPI api)
    {
        this.api = api ?? throw new ArgumentNullException(nameof(api));
    }

    /// <summary>Publishes this world's stars, planets, comets and showers.</summary>
    /// <returns>
    /// False when AstraTerra is absent or its astronomy is disabled. Nothing was published in that
    /// case, so a caller with more of the same sky to hand over should stop as well.
    /// </returns>
    public bool Publish(GalaxySky sky)
    {
        ArgumentNullException.ThrowIfNull(sky);
        var astraTerra = api.ModLoader.GetModSystem<AstraTerraModSystem>();
        if (astraTerra is null)
        {
            api.Logger.Warning("AstraExtera found no AstraTerra mod system; the procedural sky was not published.");
            return false;
        }

        var catalog = StarCatalogHandover.ToCatalog(sky);
        if (!astraTerra.ReplaceStarCatalog(catalog))
        {
            api.Logger.Warning("AstraExtera could not publish the procedural sky: AstraTerra astronomy is disabled.");
            return false;
        }

        // One sky AstraTerra keeps outside that catalog: the one a dug-up sky disc is allowed to have
        // been engraved from, held statically since asset time and filled in on the server. Left on
        // Earth's, every found disc here would come up carrying an Earth figure drawn between
        // generated stars. Handed this sky it engraves nothing instead, which is what a sky with no
        // inherited figures should give a player: a disc whose owner never engraved one.
        FoundSkyDisc.Install(catalog);

        astraTerra.ReplacePlanetCatalog(LocalSystemSkyExport.ToPlanetCatalog(sky.LocalSky));
        astraTerra.ReplaceCometCatalog(LocalSystemSkyExport.ToCometCatalog(sky.LocalSky));
        astraTerra.ReplaceMeteorShowers(LocalSystemSkyExport.ToMeteorShowers(sky.LocalSky));

        api.Logger.Event(
            "AstraExtera published the stored sky catalogs on the {0}: stars={1}; nakedEyeStars={2:0}; planets={3}; comets={4}; showers={5}; pole={6:0.0}deg from the galactic pole; host={7} {8:0.00} Msun at {9:0.00} AU.",
            api.Side,
            catalog.Stars.Count,
            sky.StarField.ExpectedVisibleCount,
            sky.LocalSky.Planets.Count,
            sky.LocalSky.Comets.Count,
            sky.LocalSky.Showers.Count,
            sky.Placement.Orientation.PoleTiltFromGalacticPoleDeg,
            sky.Placement.System.StarClassLabel,
            sky.Placement.System.StarMassSolar,
            sky.Placement.System.OrbitalDistanceAu);
        return true;
    }
}
