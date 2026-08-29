using AstraExtera.Client;
using AstraExtera.Commands;
using AstraExtera.Sync;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Server;

namespace AstraExtera;

public sealed class AstraExteraModSystem : ModSystem
{
    private GalaxyServerSync? serverSync;
    private GalaxyClientSync? clientSync;
    private GalaxyPanelController? galaxyPanel;

    public override double ExecuteOrder() => 0.6;

    public override void Start(ICoreAPI api)
    {
        api.Logger.Event(AstraExteraModMetadata.StartupLogMessage);
    }

    public override void StartServerSide(ICoreServerAPI api)
    {
        serverSync = new GalaxyServerSync(api);
        serverSync.Register();
        new GalaxyServerCommands(() => serverSync.Sky).Register(api);
        api.Logger.Event("AstraExtera startup step: galaxy and star catalog authored on the server and synced to joining players");
    }

    public override void StartClientSide(ICoreClientAPI api)
    {
        clientSync = new GalaxyClientSync(api);
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
