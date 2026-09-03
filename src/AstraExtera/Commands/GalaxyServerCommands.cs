using System.Globalization;
using AstraExtera.Galaxy;
using Vintagestory.API.Common;
using Vintagestory.API.Server;

namespace AstraExtera.Commands;

public sealed class GalaxyServerCommands
{
    private readonly Func<GalaxySky?> skyProvider;
    private readonly System.Func<long?, GalaxySky> reroll;

    public GalaxyServerCommands(Func<GalaxySky?> skyProvider, System.Func<long?, GalaxySky> reroll)
    {
        this.skyProvider = skyProvider;
        this.reroll = reroll;
    }

    public void Register(ICoreServerAPI api)
    {
        api.ChatCommands.Create("astraextera")
            .WithDescription("Inspect or reroll the server-authored cosmology used for the shared sky.")
            .RequiresPrivilege(Privilege.chat)
            .HandleWith(_ => TextCommandResult.Success("AstraExtera commands: /astraextera galaxy, /astraextera reroll [seed] (server admin)."))
            .BeginSubCommand("galaxy")
                .HandleWith(_ => TextCommandResult.Success(Describe()))
            .EndSubCommand()
            .BeginSubCommand("reroll")
                .WithDescription("Replace the saved cosmology and update every player's sky. Omit the seed to choose a new one.")
                .RequiresPrivilege(Privilege.controlserver)
                .WithArgs(api.ChatCommands.Parsers.OptionalWord("seed"))
                .HandleWith(Reroll)
            .EndSubCommand();
    }

    private TextCommandResult Reroll(TextCommandCallingArgs args)
    {
        long? seed = null;
        if (args[0] is string seedText && seedText.Length > 0)
        {
            if (!long.TryParse(seedText, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed))
            {
                return TextCommandResult.Error("Usage: /astraextera reroll [seed]. Seed must be a signed 64-bit integer.");
            }

            seed = parsed;
        }

        var previous = skyProvider();
        if (previous is null)
        {
            return TextCommandResult.Error("AstraExtera has not authored a galaxy for this save yet.");
        }

        if (seed == previous.Placement.WorldSeed)
        {
            return TextCommandResult.Success($"Cosmology seed is already {seed}. Omit the seed to roll a different sky.");
        }

        var replacement = reroll(seed);
        return TextCommandResult.Success(
            $"Cosmology rerolled: seed {previous.Placement.WorldSeed} -> {replacement.Placement.WorldSeed}. " +
            "Saved and sent to all connected players. Terrain and the world-generation seed are unchanged. " +
            "Existing constellation drawings and star names now refer to the new stars.");
    }

    private string Describe()
    {
        var sky = skyProvider();
        return sky is null
            ? "AstraExtera has not authored a galaxy for this save yet."
            : GalaxyPlacementCodec.Describe(sky);
    }
}
