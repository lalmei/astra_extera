using AstraExtera.Client;
using AstraExtera.Commands;
using AstraExtera.Config;
using AstraExtera.Sync;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Server;

namespace AstraExtera;

public sealed class AstraExteraModSystem : ModSystem
{
    private AstraExteraConfig? config;
    private GalaxyServerSync? serverSync;
    private GalaxyClientSync? clientSync;
    private GalaxyPanelController? galaxyPanel;

    public override double ExecuteOrder() => 0.6;

    public override void Start(ICoreAPI api)
    {
        api.Logger.Event(AstraExteraModMetadata.StartupLogMessage);
    }

    public override void AssetsLoaded(ICoreAPI api)
    {
        config = AstraExteraConfigLoader.Load(api);
        api.Logger.Event(
            "AstraExtera startup step: config loaded: publishNearBodyLight={0}; publishWorldObliquity={1}; maxMoonWorldObliquity={2:0.0}deg; constraints={3}",
            config.PublishNearBodyLight,
            config.PublishWorldObliquity,
            config.GetMaxMoonWorldObliquityDeg(),
            config.GetGalaxyConstraints().Describe());
    }

    public override void StartServerSide(ICoreServerAPI api)
    {
        config ??= AstraExteraConfigLoader.Load(api);
        serverSync = new GalaxyServerSync(api, config);
        serverSync.Register();
        new GalaxyServerCommands(() => serverSync.Sky, serverSync.Reroll, () => serverSync.Constraints).Register(api);
        api.Logger.Event("AstraExtera startup step: galaxy and star catalog authored on the server and synced to joining players");
    }

    public override void StartClientSide(ICoreClientAPI api)
    {
        config ??= AstraExteraConfigLoader.Load(api);
        clientSync = new GalaxyClientSync(api, config);
        clientSync.Register();
        galaxyPanel = new GalaxyPanelController(api, () => clientSync.Sky);
        galaxyPanel.Register();
        api.Logger.Event("AstraExtera startup step: waiting for the server galaxy placement");
    }

    public override void Dispose()
    {
        serverSync?.Unregister();
        clientSync?.Unregister();
        galaxyPanel?.Dispose();
    }
}
