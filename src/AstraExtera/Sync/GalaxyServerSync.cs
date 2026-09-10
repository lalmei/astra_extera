using AstraExtera.Config;
using AstraExtera.Galaxy;
using Vintagestory.API.Server;

namespace AstraExtera.Sync;

public sealed class GalaxyServerSync
{
    private readonly ICoreServerAPI api;
    private readonly AstraTerraCatalogBridge catalogBridge;
    private readonly AstraTerraWorldBridge worldBridge;
    private readonly GalaxyConstraints constraints;
    private IServerNetworkChannel? channel;
    private GalaxySky? sky;

    public GalaxyServerSync(ICoreServerAPI api, AstraExteraConfig config)
    {
        this.api = api;
        catalogBridge = new AstraTerraCatalogBridge(api);
        worldBridge = new AstraTerraWorldBridge(api, config);
        constraints = config.GetGalaxyConstraints();
    }

    /// <summary>What this server asks the generator for, for authoring and for reroll alike.</summary>
    public GalaxyConstraints Constraints => constraints;

    public GalaxySky? Sky => sky;

    public GalaxyPlacement? Placement => sky?.Placement;

    public GalaxySky Reroll(long? seed = null)
    {
        if (sky is null || channel is null)
        {
            throw new InvalidOperationException("AstraExtera has not loaded this save's cosmology yet.");
        }

        var previousSeed = sky.Placement.WorldSeed;
        var nextSeed = seed ?? Random.Shared.NextInt64();
        while (seed is null && nextSeed == previousSeed)
        {
            nextSeed = Random.Shared.NextInt64();
        }

        var replacement = GalaxySky.Author(nextSeed, constraints, out var outcome);
        Report(outcome);
        var packet = ToPacket(replacement);
        Store(packet);
        sky = replacement;
        Publish(replacement);
        channel.BroadcastPacket(packet);
        api.Logger.Event("AstraExtera rerolled cosmology: seed {0} -> {1}.", previousSeed, nextSeed);
        api.Logger.Event(GalaxyPlacementCodec.Describe(replacement));
        return replacement;
    }

    public void Register()
    {
        channel = api.Network.RegisterChannel(AstraExteraModMetadata.GalaxyChannelName)
            .RegisterMessageType<GalaxyPlacementPacket>();
        api.Event.SaveGameLoaded += OnSaveGameLoaded;
        api.Event.PlayerJoin += OnPlayerJoin;
    }

    public void Unregister()
    {
        api.Event.SaveGameLoaded -= OnSaveGameLoaded;
        api.Event.PlayerJoin -= OnPlayerJoin;
    }

    private void OnSaveGameLoaded()
    {
        sky = LoadOrGenerate();
        api.Logger.Event(GalaxyPlacementCodec.Describe(sky));
        Publish(sky);
    }

    /// <summary>
    /// Tells this server's own AstraTerra what sky this save has.
    /// </summary>
    /// <remarks>
    /// The painted near bodies are the client's business and are published there. These are not.
    /// AstraTerra reads its star catalog on the server to validate the constellations players
    /// submit, to fill prepared books, and to answer <c>/stars</c>, so a server still holding Earth's
    /// shipped catalog would be checking figures against a sky nobody can see. And what lights the
    /// ground is what decides whether things spawn on it, while how far the world is tipped is what
    /// decides how long its days are, so a server that knew neither would be running different rules
    /// from the world its players are looking at.
    /// </remarks>
    private void Publish(GalaxySky published)
    {
        catalogBridge.Publish(published);
        worldBridge.Publish(published.Placement);
    }

    private void OnPlayerJoin(IServerPlayer player)
    {
        if (sky is null || channel is null)
        {
            return;
        }

        channel.SendPacket(ToPacket(sky), player);
    }

    private GalaxySky LoadOrGenerate()
    {
        var resolution = GalaxySkyStore.Resolve(
            TryLoadPlacement(),
            TryLoadStars(),
            api.World.Seed,
            TryLoadLocalSky(),
            constraints,
            out var outcome);
        Report(outcome);
        if (resolution.PlacementDirty || resolution.StarsDirty || resolution.LocalSkyDirty)
        {
            Store(ToPacket(resolution.Sky), resolution.PlacementDirty, resolution.StarsDirty, resolution.LocalSkyDirty);
        }

        return resolution.Sky;
    }

    /// <summary>
    /// Says out loud what the generator was asked for and whether it managed it. A constraint that
    /// could not be met produced a world anyway, and an operator staring at a sky that is not the
    /// one they configured needs the log to tell them which setting to widen.
    /// </summary>
    private void Report(GalaxyConstraintOutcome? outcome)
    {
        if (outcome is null)
        {
            return;
        }

        foreach (var message in outcome.Messages())
        {
            api.Logger.Warning(message);
        }
    }

    private void Store(
        GalaxyPlacementPacket packet,
        bool placementDirty = true,
        bool starsDirty = true,
        bool localSkyDirty = true)
    {
        if (placementDirty)
        {
            api.WorldManager.SaveGame.StoreData(
                AstraExteraModMetadata.GalaxySaveKey,
                packet.Payload);
        }

        if (starsDirty)
        {
            api.WorldManager.SaveGame.StoreData(
                AstraExteraModMetadata.StarFieldSaveKey,
                packet.StarFieldPayload);
        }

        if (localSkyDirty)
        {
            api.WorldManager.SaveGame.StoreData(
                AstraExteraModMetadata.LocalSkySaveKey,
                packet.LocalSkyPayload);
        }
    }

    private GalaxyPlacement? TryLoadPlacement()
    {
        var stored = api.WorldManager.SaveGame.GetData(AstraExteraModMetadata.GalaxySaveKey);
        if (stored is not { Length: > 0 })
        {
            return null;
        }

        try
        {
            return GalaxyPlacementCodec.FromUtf8(stored);
        }
        catch (Exception exception)
        {
            api.Logger.Warning("AstraExtera ignored stored galaxy placement: {0}", exception.Message);
            return null;
        }
    }

    private StarField? TryLoadStars()
    {
        var stored = api.WorldManager.SaveGame.GetData(AstraExteraModMetadata.StarFieldSaveKey);
        if (stored is not { Length: > 0 })
        {
            return null;
        }

        try
        {
            return StarFieldCodec.FromBytes(stored);
        }
        catch (Exception exception)
        {
            api.Logger.Warning("AstraExtera ignored stored star catalog: {0}", exception.Message);
            return null;
        }
    }

    private LocalSystemSky? TryLoadLocalSky()
    {
        var stored = api.WorldManager.SaveGame.GetData(AstraExteraModMetadata.LocalSkySaveKey);
        if (stored is not { Length: > 0 })
        {
            return null;
        }

        try
        {
            return LocalSystemSkyCodec.FromUtf8(stored);
        }
        catch (Exception exception)
        {
            api.Logger.Warning("AstraExtera ignored stored local sky: {0}", exception.Message);
            return null;
        }
    }

    private static GalaxyPlacementPacket ToPacket(GalaxySky sky)
        => new()
        {
            Payload = GalaxyPlacementCodec.ToUtf8(sky.Placement),
            StarFieldPayload = StarFieldCodec.ToBytes(sky.StarField),
            LocalSkyPayload = LocalSystemSkyCodec.ToUtf8(sky.LocalSky)
        };
}
