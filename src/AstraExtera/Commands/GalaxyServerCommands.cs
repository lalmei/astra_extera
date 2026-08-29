using AstraExtera.Galaxy;
using Vintagestory.API.Common;
using Vintagestory.API.Server;

namespace AstraExtera.Commands;

public sealed class GalaxyServerCommands
{
    private readonly Func<GalaxySky?> skyProvider;

    public GalaxyServerCommands(Func<GalaxySky?> skyProvider)
    {
        this.skyProvider = skyProvider;
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
        var sky = skyProvider();
        return sky is null
            ? "AstraExtera has not authored a galaxy for this save yet."
            : GalaxyPlacementCodec.Describe(sky);
    }
}
