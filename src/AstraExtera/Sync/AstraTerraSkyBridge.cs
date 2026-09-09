using AstraExtera.Client;
using AstraExtera.Config;
using AstraExtera.Galaxy;
using AstraTerra;
using Vintagestory.API.Client;

namespace AstraExtera.Sync;

/// <summary>
/// Hands this world's stored sky to AstraTerra from a client: the catalogs of everything in it, and
/// then the near bodies, which are the part that has to be painted.
/// </summary>
/// <remarks>
/// <para>
/// The catalogs themselves go through <see cref="AstraTerraCatalogBridge"/>, because the server
/// publishes those too; what belongs here is the drawing. Near-body entries carry a composited face
/// per body, so they need a client's asset and texture machinery. What that same giant does to the
/// light, and the axis it locks this world to, go through <see cref="AstraTerraWorldBridge"/> on both
/// sides, because they decide what spawns rather than what is drawn.
/// </para>
/// <para>
/// AstraTerra loads its shipped Earth catalogs from assets before a client knows which world it is
/// joining, so the swap can only happen once the server's galaxy packet has arrived.
/// </para>
/// </remarks>
public sealed class AstraTerraSkyBridge
{
    private readonly ICoreClientAPI api;
    private readonly CelestialTextureLibrary textures;
    private readonly AstraTerraCatalogBridge catalogBridge;
    private readonly AstraTerraWorldBridge worldBridge;
    private long? publishedSeed;

    public AstraTerraSkyBridge(ICoreClientAPI api, AstraExteraConfig config)
    {
        this.api = api;
        textures = new CelestialTextureLibrary(api);
        catalogBridge = new AstraTerraCatalogBridge(api);
        worldBridge = new AstraTerraWorldBridge(api, config);
    }

    public void Publish(GalaxySky sky)
    {
        ArgumentNullException.ThrowIfNull(sky);
        if (publishedSeed == sky.Placement.WorldSeed)
        {
            return;
        }

        if (!catalogBridge.Publish(sky))
        {
            return;
        }

        // The catalogs went out, so AstraTerra is here and its astronomy is on. Same lookup again,
        // this time for the half of the sky that has to be painted before it can be handed over.
        var astraTerra = api.ModLoader.GetModSystem<AstraTerraModSystem>();
        if (astraTerra is null)
        {
            return;
        }

        // No world here gets Earth's moon. A moon world gets the giant it orbits, fixed in one spot
        // because it is tidally locked to it, and its sibling moons; a planet world gets the moons
        // the generator gave it, which on some worlds is none.
        var bodies = NearSky.Author(sky.Placement);
        var nearBodies = NearBodyExport.Build(sky.Placement, textures);
        astraTerra.ReplaceNearBodies(nearBodies);

        // And the half of the same giant that is not a picture: what it does to the light, and the
        // axis this world turns on because it is locked to it. The server publishes these too --
        // see AstraTerraWorldBridge -- because they decide what spawns rather than what is drawn.
        worldBridge.Publish(sky.Placement, bodies);

        publishedSeed = sky.Placement.WorldSeed;
        api.Logger.Event(
            "AstraExtera published the stored sky's near bodies: nearBodies={0} (vanilla moon hidden={1}).",
            nearBodies.Bodies.Count,
            nearBodies.HidesVanillaMoon);
    }
}
