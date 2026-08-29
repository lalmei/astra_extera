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
        if (resolution.PlacementDirty)
        {
            api.WorldManager.SaveGame.StoreData(
                AstraExteraModMetadata.GalaxySaveKey,
                GalaxyPlacementCodec.ToUtf8(resolution.Sky.Placement));
        }

        if (resolution.StarsDirty)
        {
            api.WorldManager.SaveGame.StoreData(
                AstraExteraModMetadata.StarFieldSaveKey,
                StarFieldCodec.ToBytes(resolution.Sky.StarField));
        }

        if (resolution.LocalSkyDirty)
        {
            api.WorldManager.SaveGame.StoreData(
                AstraExteraModMetadata.LocalSkySaveKey,
                LocalSystemSkyCodec.ToUtf8(resolution.Sky.LocalSky));
        }

        return resolution.Sky;
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
