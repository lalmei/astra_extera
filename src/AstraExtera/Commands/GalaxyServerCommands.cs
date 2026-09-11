using System.Globalization;
using AstraExtera.Galaxy;
using Vintagestory.API.Common;
using Vintagestory.API.Server;

namespace AstraExtera.Commands;

public sealed class GalaxyServerCommands
{
    /// <summary>
    /// The word that turns a described reroll into a performed one.
    /// </summary>
    /// <remarks>
    /// Confirmation is a repeat of the whole command rather than a remembered "are you sure?". A
    /// pending-confirmation state would have to be keyed to a caller and expired, and two admins in
    /// the same console would share it; typing the seed again costs nothing and cannot confirm
    /// somebody else's reroll.
    /// </remarks>
    public const string ConfirmWord = "confirm";

    private readonly Func<GalaxySky?> skyProvider;
    private readonly System.Func<long?, GalaxySky> reroll;
    private readonly Func<GalaxyConstraints> constraintsProvider;

    public GalaxyServerCommands(
        Func<GalaxySky?> skyProvider,
        System.Func<long?, GalaxySky> reroll,
        Func<GalaxyConstraints>? constraintsProvider = null)
    {
        this.skyProvider = skyProvider;
        this.reroll = reroll;
        this.constraintsProvider = constraintsProvider ?? (static () => GalaxyConstraints.Unconstrained);
    }

    public void Register(ICoreServerAPI api)
    {
        api.ChatCommands.Create("astraextera")
            .WithDescription("Inspect, search for, or reroll the server-authored cosmology used for the shared sky.")
            .RequiresPrivilege(Privilege.chat)
            .HandleWith(_ => TextCommandResult.Success(
                "AstraExtera commands: /astraextera galaxy, and for a server admin "
                + "/astraextera preview [seed], /astraextera find <constraints>, "
                + $"/astraextera reroll [seed] {ConfirmWord}."))
            .BeginSubCommand("galaxy")
                .HandleWith(_ => TextCommandResult.Success(Describe()))
            .EndSubCommand()
            .BeginSubCommand("preview")
                .WithDescription("Author a candidate sky and print it without saving it or sending it to anyone.")
                .RequiresPrivilege(Privilege.controlserver)
                .WithArgs(api.ChatCommands.Parsers.OptionalWord("seed"))
                .HandleWith(Preview)
            .EndSubCommand()
            .BeginSubCommand("find")
                .WithDescription("Draw seeds until one matches, e.g. find kind=moon star=K rings=required, and report it without applying it.")
                .RequiresPrivilege(Privilege.controlserver)
                .WithArgs(api.ChatCommands.Parsers.OptionalAll("constraints"))
                .HandleWith(Find)
            .EndSubCommand()
            .BeginSubCommand("reroll")
                .WithDescription("Replace the saved cosmology and update every player's sky. Omit the seed to choose a new one.")
                .RequiresPrivilege(Privilege.controlserver)
                .WithArgs(
                    api.ChatCommands.Parsers.OptionalWord("seed"),
                    api.ChatCommands.Parsers.OptionalWord(ConfirmWord))
                .HandleWith(Reroll)
            .EndSubCommand();
    }

    /// <summary>
    /// Authors a sky and prints it, storing nothing and broadcasting nothing.
    /// </summary>
    /// <remarks>
    /// This authors the sky the same way <see cref="GalaxyServerSync.Reroll"/> does, under the same
    /// server constraints, so previewing a seed and then rerolling it gives the sky that was
    /// previewed. The whole destructive half of a reroll is storing and broadcasting the result,
    /// and neither happens here: <see cref="GalaxySky.Author(long, GalaxyConstraints?)"/> is
    /// deterministic and touches nothing.
    /// </remarks>
    private TextCommandResult Preview(TextCommandCallingArgs args)
    {
        if (!TryReadSeed(args[0] as string, out var seed, out var seedError))
        {
            return TextCommandResult.Error($"Usage: /astraextera preview [seed]. {seedError}");
        }

        var chosen = seed ?? Random.Shared.NextInt64();
        var candidate = GalaxySky.Author(chosen, constraintsProvider(), out var outcome);
        var lines = new List<string>
        {
            $"Preview of seed {chosen}. Nothing was saved and no player's sky changed."
        };

        lines.AddRange(outcome.Messages());
        lines.Add(GalaxyPlacementCodec.Describe(candidate));
        lines.Add($"Apply it with /astraextera reroll {chosen} {ConfirmWord}.");
        return TextCommandResult.Success(string.Join("\n", lines));
    }

    /// <summary>Hunts for a seed that satisfies what was typed, and reports it without applying it.</summary>
    private TextCommandResult Find(TextCommandCallingArgs args)
    {
        var typed = args[0] as string;
        if (string.IsNullOrWhiteSpace(typed))
        {
            return TextCommandResult.Error(
                $"Usage: /astraextera find kind=moon star=K rings=required. {GalaxyConstraints.Usage}");
        }

        if (!GalaxyConstraints.TryParse(typed, out var wanted, out var parseError))
        {
            return TextCommandResult.Error($"Usage: /astraextera find <constraints>. {parseError}");
        }

        if (wanted.IsUnconstrained)
        {
            return TextCommandResult.Error(
                "That asks for nothing, so every seed matches it. Name a constraint, or use "
                + $"/astraextera preview to look at a sky. {GalaxyConstraints.Usage}");
        }

        // A config widens a contradiction and warns, because a save still has to load. Someone who
        // typed one has asked a question with no answer, and is better told than left waiting.
        var contradictions = wanted.Contradictions();
        if (contradictions.Count > 0)
        {
            return TextCommandResult.Error(
                $"No sky can satisfy '{wanted.Describe()}'. {string.Join(" ", contradictions)}");
        }

        var authoring = constraintsProvider();
        var result = GalaxySkySearch.Find(wanted, authoring);
        var underConfig = authoring.IsUnconstrained
            ? string.Empty
            : $" Seeds were drawn under this server's configured constraints '{authoring.Describe()}', "
              + "which is how a reroll will generate them.";

        if (!result.Found)
        {
            var why = result.Unsatisfied.Count > 0
                ? $"Nothing drawn satisfied {string.Join(" or ", result.Unsatisfied)}, even on its own."
                : "Each constraint turned up on its own, so the combination is rare rather than "
                  + "impossible; run it again or drop one.";
            return TextCommandResult.Error(
                $"No sky matching '{wanted.Describe()}' in {result.Attempts} seeds. {why}{underConfig}");
        }

        var match = result.Match!;
        return TextCommandResult.Success(string.Join(
            "\n",
            $"Found '{wanted.Describe()}' at seed {match.WorldSeed} after {result.Attempts} "
            + $"seed{(result.Attempts == 1 ? string.Empty : "s")}. Nothing was applied.{underConfig}",
            GalaxyPlacementCodec.Describe(match),
            $"Look at the whole sky with /astraextera preview {match.WorldSeed}, then apply it with "
            + $"/astraextera reroll {match.WorldSeed} {ConfirmWord}."));
    }

    private TextCommandResult Reroll(TextCommandCallingArgs args)
    {
        // Either "reroll <seed> confirm" or the seedless "reroll confirm", so the first word is a
        // seed only when it is not the confirmation.
        var first = args[0] as string;
        var second = args[1] as string;
        var confirmedFirst = IsConfirm(first);
        var confirmed = confirmedFirst || IsConfirm(second);
        var seedText = confirmedFirst ? null : first;

        if (!string.IsNullOrEmpty(second) && !IsConfirm(second))
        {
            return TextCommandResult.Error(
                $"Usage: /astraextera reroll [seed] [{ConfirmWord}]. '{second}' is not '{ConfirmWord}'.");
        }

        if (confirmedFirst && !string.IsNullOrEmpty(second))
        {
            return TextCommandResult.Error(
                $"Usage: /astraextera reroll [seed] [{ConfirmWord}]. The seed comes before '{ConfirmWord}'.");
        }

        if (!TryReadSeed(seedText, out var seed, out var seedError))
        {
            return TextCommandResult.Error($"Usage: /astraextera reroll [seed] [{ConfirmWord}]. {seedError}");
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

        if (!confirmed)
        {
            return TextCommandResult.Error(Consequences(seed));
        }

        var replacement = reroll(seed);
        return TextCommandResult.Success(
            $"Cosmology rerolled: seed {previous.Placement.WorldSeed} -> {replacement.Placement.WorldSeed}. " +
            "Saved and sent to all connected players. Terrain and the world-generation seed are unchanged. " +
            "Existing constellation drawings and star names now refer to the new stars.");
    }

    /// <summary>
    /// What a reroll costs, said before it happens rather than in the message afterwards.
    /// </summary>
    /// <remarks>
    /// The drawings cannot be carried across, and this says so rather than offering to try. A
    /// constellation is a set of star IDs, and those IDs are positions in a catalog sorted by
    /// brightness over a freshly sampled sky -- star 12 of one sky and star 12 of the next are not
    /// the same star and are not near each other. There is no correspondence to migrate along: the
    /// new sky does not contain the old sky's stars at all.
    /// </remarks>
    private static string Consequences(long? seed)
    {
        var seedArgs = seed is null ? string.Empty : $" {seed}";
        var look = seed is null
            ? "A seedless reroll takes whatever it draws; look at candidates with /astraextera preview, "
              + "or search for one with /astraextera find."
            : $"Look at it first with /astraextera preview{seedArgs}.";

        return
            "A reroll replaces this save's galaxy, star field and local sky for every player. "
            + "Star IDs are positions in a catalog sorted by brightness, so a new sky renumbers them: "
            + "existing constellation drawings and star names cannot be migrated and will connect and "
            + "label unrelated stars. Terrain and the world-generation seed are unchanged. "
            + $"{look} Run /astraextera reroll{seedArgs} {ConfirmWord} to go ahead.";
    }

    private static bool IsConfirm(string? word)
        => word is not null && word.Equals(ConfirmWord, StringComparison.OrdinalIgnoreCase);

    private static bool TryReadSeed(string? text, out long? seed, out string error)
    {
        seed = null;
        error = string.Empty;
        if (string.IsNullOrEmpty(text))
        {
            return true;
        }

        if (!long.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed))
        {
            error = "Seed must be a signed 64-bit integer.";
            return false;
        }

        seed = parsed;
        return true;
    }

    private string Describe()
    {
        var sky = skyProvider();
        return sky is null
            ? "AstraExtera has not authored a galaxy for this save yet."
            : GalaxyPlacementCodec.Describe(sky);
    }
}
