namespace AstraExtera.Galaxy;

/// <summary>
/// The server-authored sky: a galactic placement, the sampled star catalog, and the local system's
/// wanderers.
/// <para>
/// Sampling runs once, on the server, when the save is first authored (or when a current placement
/// is loaded without a stored catalog). Clients render those stored lists rather than drawing their
/// own, so every player sees the same stars, planets, comets and showers.
/// </para>
/// </summary>
public sealed record GalaxySky(GalaxyPlacement Placement, StarField StarField, LocalSystemSky LocalSky)
{
    public static GalaxySky Author(long worldSeed)
        => Author(GalaxyGenerator.Generate(worldSeed));

    public static GalaxySky Author(GalaxyPlacement placement)
    {
        ArgumentNullException.ThrowIfNull(placement);
        return new GalaxySky(
            placement,
            StarFieldCodec.Quantize(StarFieldSampler.Sample(placement)),
            LocalSystemSky.Author(placement));
    }
}

/// <summary>
/// Decides whether a save already has a sky or still needs one sampled.
/// </summary>
public sealed record GalaxySkyResolution(
    GalaxySky Sky,
    bool PlacementDirty,
    bool StarsDirty,
    bool LocalSkyDirty);

/// <summary>
/// Persistence policy for <see cref="GalaxySky"/>. Placement schema still gates regenerating the
/// galaxy; a current placement with no stored stars or wanderers is a one-time migration sample.
/// </summary>
public static class GalaxySkyStore
{
    public static GalaxySkyResolution Resolve(
        GalaxyPlacement? storedPlacement,
        StarField? storedStars,
        long worldSeed,
        LocalSystemSky? storedLocalSky = null)
    {
        if (storedPlacement is not null
            && storedPlacement.SchemaVersion == GalaxyPlacement.CurrentSchemaVersion)
        {
            var starsDirty = storedStars is null;
            var localDirty = storedLocalSky is null;
            var stars = storedStars ?? StarFieldCodec.Quantize(StarFieldSampler.Sample(storedPlacement));
            var localSky = storedLocalSky ?? LocalSystemSky.Author(storedPlacement);
            return new GalaxySkyResolution(
                new GalaxySky(storedPlacement, stars, localSky),
                false,
                starsDirty,
                localDirty);
        }

        return new GalaxySkyResolution(GalaxySky.Author(worldSeed), true, true, true);
    }
}
