using System.Text.Json;
using System.Text.Json.Serialization;

namespace AstraExtera.Galaxy;

public static class GalaxyPlacementCodec
{
    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false,
        Converters = { new JsonStringEnumConverter() }
    };

    public static byte[] ToUtf8(GalaxyPlacement placement)
        => JsonSerializer.SerializeToUtf8Bytes(placement, Options);

    public static GalaxyPlacement FromUtf8(byte[] utf8)
        => JsonSerializer.Deserialize<GalaxyPlacement>(utf8, Options)
           ?? throw new InvalidOperationException("Galaxy placement JSON was empty.");

    public static string Describe(GalaxyPlacement placement)
    {
        var kind = placement.WorldKind == ObserverWorldKind.TerrestrialMoon
            ? "terrestrial moon"
            : "terrestrial planet";
        var morphology = placement.Galaxy.MorphologyLabel;
        return
            $"AstraExtera galaxy: seed={placement.WorldSeed}; {morphology}; " +
            $"R={placement.Location.GalactocentricRadiusKpc:0.00} kpc; " +
            $"|z|={Math.Abs(placement.Location.HeightPc):0} pc; " +
            $"[Fe/H]={placement.Location.MetallicityFeH:+0.00;-0.00}; " +
            $"iron={placement.CanHostIronCore}; ores={placement.CanHostOres}; " +
            $"world={kind}; star={placement.System.StarClassLabel} {placement.System.StarMassSolar:0.00} Msun; " +
            $"a={placement.System.OrbitalDistanceAu:0.00} AU; " +
            $"Rearth={placement.World.RadiusEarth:0.00}; g={placement.World.SurfaceGravityG:0.00}; " +
            $"Fe={placement.World.BulkIronMassFraction:0.00}; T={placement.World.SurfaceTemperatureK:0} K.";
    }

    public static string Describe(GalaxySky sky)
    {
        ArgumentNullException.ThrowIfNull(sky);
        return
            $"{Describe(sky.Placement)} " +
            $"stars={sky.StarField.Stars.Count}; nakedEye={sky.StarField.ExpectedVisibleCount:0}; " +
            $"planets={sky.LocalSky.Planets.Count}; comets={sky.LocalSky.Comets.Count}; " +
            $"showers={sky.LocalSky.Showers.Count}.";
    }
}
