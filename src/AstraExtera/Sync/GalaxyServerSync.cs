using AstraExtera.Galaxy;
using Vintagestory.API.Server;

namespace AstraExtera.Sync;

public sealed class GalaxyServerSync
{
    private readonly ICoreServerAPI api;
    private IServerNetworkChannel? channel;
    private GalaxyPlacement? placement;

    public GalaxyServerSync(ICoreServerAPI api)
    {
        this.api = api;
    }

    public GalaxyPlacement? Placement => placement;

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
        placement = LoadOrGenerate();
        api.Logger.Event(GalaxyPlacementCodec.Describe(placement));
    }

    private void OnPlayerJoin(IServerPlayer player)
    {
        if (placement is null || channel is null)
        {
            return;
        }

        channel.SendPacket(new GalaxyPlacementPacket { Payload = GalaxyPlacementCodec.ToUtf8(placement) }, player);
    }

    private GalaxyPlacement LoadOrGenerate()
    {
        var stored = api.WorldManager.SaveGame.GetData(AstraExteraModMetadata.GalaxySaveKey);
        if (stored is { Length: > 0 })
        {
            try
            {
                var loaded = GalaxyPlacementCodec.FromUtf8(stored);
                if (loaded.SchemaVersion == GalaxyPlacement.CurrentSchemaVersion)
                {
                    return loaded;
                }
            }
            catch (Exception exception)
            {
                api.Logger.Warning("AstraExtera ignored stored galaxy placement: {0}", exception.Message);
            }
        }

        var generated = GalaxyGenerator.Generate(api.World.Seed);
        api.WorldManager.SaveGame.StoreData(AstraExteraModMetadata.GalaxySaveKey, GalaxyPlacementCodec.ToUtf8(generated));
        return generated;
    }
}
