using AstraExtera.Galaxy;
using Vintagestory.API.Common;
using Vintagestory.API.Server;

namespace AstraExtera.Commands;

public sealed class GalaxyServerCommands
{
    private readonly Func<GalaxyPlacement?> placementProvider;

    public GalaxyServerCommands(Func<GalaxyPlacement?> placementProvider)
    {
        this.placementProvider = placementProvider;
    }

    public void Register(ICoreServerAPI api)
    {
        api.ChatCommands.Create("astraextera")
            .WithDescription("Inspect the server-authored galaxy used for the shared sky.")
            .RequiresPrivilege(Privilege.chat)
            .HandleWith(_ => TextCommandResult.Success("AstraExtera commands: /astraextera galaxy"))
            .BeginSubCommand("galaxy")
                .HandleWith(_ => TextCommandResult.Success(Describe()))
            .EndSubCommand();
    }

    private string Describe()
    {
        var placement = placementProvider();
        return placement is null
            ? "AstraExtera has not authored a galaxy for this save yet."
            : GalaxyPlacementCodec.Describe(placement);
    }
}
