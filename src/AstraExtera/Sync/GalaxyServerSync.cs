using AstraExtera.Galaxy;
using Vintagestory.API.Server;

namespace AstraExtera.Sync;

public sealed class GalaxyServerSync
{
    private readonly ICoreServerAPI api;
    private IServerNetworkChannel? channel;
    private GalaxySky? sky;

    public GalaxyServerSync(ICoreServerAPI api)
    {
        this.api = api;
    }

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

        var replacement = GalaxySky.Author(nextSeed);
        var packet = ToPacket(replacement);
        Store(packet);
        sky = replacement;
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
            TryLoadLocalSky());
        if (resolution.PlacementDirty || resolution.StarsDirty || resolution.LocalSkyDirty)
        {
            Store(ToPacket(resolution.Sky), resolution.PlacementDirty, resolution.StarsDirty, resolution.LocalSkyDirty);
        }

        return resolution.Sky;
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
