using AstraExtera.Galaxy;
using Vintagestory.API.Client;

namespace AstraExtera.Sync;

public sealed class GalaxyClientSync
{
    private readonly ICoreClientAPI api;
    private GalaxyPlacement? placement;

    public GalaxyClientSync(ICoreClientAPI api)
    {
        this.api = api;
    }

    public GalaxyPlacement? Placement => placement;

    public void Register()
    {
        api.Network.RegisterChannel(AstraExteraModMetadata.GalaxyChannelName)
            .RegisterMessageType<GalaxyPlacementPacket>()
            .SetMessageHandler<GalaxyPlacementPacket>(OnPacket);
    }

    private void OnPacket(GalaxyPlacementPacket packet)
    {
        try
        {
            placement = GalaxyPlacementCodec.FromUtf8(packet.Payload);
            api.Logger.Event(GalaxyPlacementCodec.Describe(placement));
        }
        catch (Exception exception)
        {
            api.Logger.Error("AstraExtera failed to apply galaxy placement: {0}", exception);
        }
    }
}
