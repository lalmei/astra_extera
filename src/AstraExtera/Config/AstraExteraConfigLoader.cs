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
    }
}
