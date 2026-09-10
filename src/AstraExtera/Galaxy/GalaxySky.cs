namespace AstraExtera.Galaxy;

/// <summary>
/// The server-authored sky: a galactic placement, the sampled star catalog, and the local system's
/// wanderers.
/// <para>
/// Sampling runs on the server when the save is first authored, an admin rerolls the cosmology,
/// or a current placement is loaded without a stored catalog. Clients render those lists rather
/// than drawing their own, so every player sees the same stars, planets, comets and showers.
/// </para>
/// </summary>
public sealed record GalaxySky(GalaxyPlacement Placement, StarField StarField, LocalSystemSky LocalSky)
{
    public static GalaxySky Author(long worldSeed)
        => Author(GalaxyGenerator.Generate(worldSeed));

    public static GalaxySky Author(long worldSeed, GalaxyConstraints? constraints)
        => Author(worldSeed, constraints, out _);

    /// <summary>
    /// Authors a sky under a server's constraints, reporting through <paramref name="outcome"/>
    /// whether they could be met so the caller can log it.
    /// </summary>
    public static GalaxySky Author(
        long worldSeed,
        GalaxyConstraints? constraints,
        out GalaxyConstraintOutcome outcome)
        => Author(GalaxyGenerator.Generate(worldSeed, constraints, out outcome));

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
        LocalSystemSky? storedLocalSky = null,
        GalaxyConstraints? constraints = null)
        => Resolve(storedPlacement, storedStars, worldSeed, storedLocalSky, constraints, out _);

    /// <summary>
    /// As above, and reports how a fresh authoring fared against <paramref name="constraints"/>.
    /// A save that already has a placement never consults them: the sky it is holding was authored
    /// under whatever was configured at the time, and the config has no business rewriting it.
    /// </summary>
    public static GalaxySkyResolution Resolve(
        GalaxyPlacement? storedPlacement,
        StarField? storedStars,
        long worldSeed,
        LocalSystemSky? storedLocalSky,
        GalaxyConstraints? constraints,
        out GalaxyConstraintOutcome? outcome)
    {
        outcome = null;
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

        // A rerolled cosmology keeps its own seed even when a schema upgrade rebuilds the sky.
        var authored = GalaxySky.Author(storedPlacement?.WorldSeed ?? worldSeed, constraints, out var authoring);
        outcome = authoring;
        return new GalaxySkyResolution(authored, true, true, true);
    }
}
