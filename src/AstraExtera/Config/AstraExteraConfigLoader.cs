using Vintagestory.API.Common;

namespace AstraExtera.Config;

public static class AstraExteraConfigLoader
{
    private const string ConfigName = "astraextera.json";

    public static AstraExteraConfig Load(ICoreAPI api)
    {
        ArgumentNullException.ThrowIfNull(api);
        var config = api.LoadModConfig<AstraExteraConfig>(ConfigName) ?? new AstraExteraConfig();
        Normalize(config, api);
        api.StoreModConfig(config, ConfigName);

        return config;
    }

    private static void Normalize(AstraExteraConfig config, ICoreAPI api)
    {
        var maxObliquity = config.GetMaxMoonWorldObliquityDeg();
        if (maxObliquity != config.MaxMoonWorldObliquityDeg)
        {
            api.Logger.Warning(
                "AstraExtera config has an out-of-range moon world obliquity cap '{0}'; using '{1}'.",
                config.MaxMoonWorldObliquityDeg,
                maxObliquity);
        }

        config.MaxMoonWorldObliquityDeg = maxObliquity;

        // A misspelt constraint is silently ignored by the generator, so it has to be said out loud
        // here: an operator who wrote "K-type" and got a random star deserves to know why.
        var constraints = config.GetGalaxyConstraints(out var rejected);
        foreach (var complaint in rejected)
        {
            api.Logger.Warning("AstraExtera config: {0}", complaint);
        }

        // Contradictions are reported at load rather than only at authoring time, so a server owner
        // sees them when they edit the file rather than the next time a world is generated.
        foreach (var warning in constraints.Reconcile().Warnings)
        {
            api.Logger.Warning("AstraExtera config: {0}", warning);
        }
    }
}
