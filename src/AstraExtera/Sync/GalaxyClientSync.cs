using AstraExtera.Client;
using AstraExtera.Galaxy;
using Vintagestory.API.Client;
using Vintagestory.API.Common;

namespace AstraExtera.Sync;

public sealed class GalaxyClientSync
{
    private readonly ICoreClientAPI api;
    private readonly AstraTerraSkyBridge skyBridge;
    private readonly GalaxyGlowRenderer glowRenderer;
    private GalaxySky? sky;

    public GalaxyClientSync(ICoreClientAPI api)
    {
        this.api = api;
        skyBridge = new AstraTerraSkyBridge(api);
        glowRenderer = new GalaxyGlowRenderer(api);
    }

    public GalaxySky? Sky => sky;

    public GalaxyPlacement? Placement => sky?.Placement;

    public void Register()
    {
        api.Network.RegisterChannel(AstraExteraModMetadata.GalaxyChannelName)
            .RegisterMessageType<GalaxyPlacementPacket>()
            .SetMessageHandler<GalaxyPlacementPacket>(OnPacket);
        api.Event.RegisterRenderer(glowRenderer, EnumRenderStage.Opaque, "AstraExteraGalaxyGlow");
    }

    public void Unregister()
    {
        api.Event.UnregisterRenderer(glowRenderer, EnumRenderStage.Opaque);
        glowRenderer.Dispose();
    }

    private void OnPacket(GalaxyPlacementPacket packet)
    {
        try
        {
            sky = FromPacket(packet);
            api.Logger.Event(GalaxyPlacementCodec.Describe(sky));
            skyBridge.Publish(sky);
            glowRenderer.Apply(sky.Placement);
        }
        catch (Exception exception)
        {
            api.Logger.Error("AstraExtera failed to apply galaxy placement: {0}", exception);
        }
    }

    private static GalaxySky FromPacket(GalaxyPlacementPacket packet)
    {
        var placement = GalaxyPlacementCodec.FromUtf8(packet.Payload);
        if (packet.StarFieldPayload is not { Length: > 0 })
        {
            throw new InvalidOperationException(
                "The server sent a galaxy placement without a stored star catalog.");
        }

        if (packet.LocalSkyPayload is not { Length: > 0 })
        {
            throw new InvalidOperationException(
                "The server sent a galaxy placement without a stored local sky.");
        }

        return new GalaxySky(
            placement,
            StarFieldCodec.FromBytes(packet.StarFieldPayload),
            LocalSystemSkyCodec.FromUtf8(packet.LocalSkyPayload));
    }
}
