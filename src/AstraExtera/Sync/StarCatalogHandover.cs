using AstraExtera.Galaxy;
using AstraTerra.Astronomy;

namespace AstraExtera.Sync;

/// <summary>
/// Turns this world's stored star field into the catalog AstraTerra draws and validates against.
/// </summary>
/// <remarks>
/// <para>
/// Separate from <see cref="StarCatalogExport"/>, which writes the same stars as the JSON asset
/// shape for tools and tests. This is the runtime handover, and the only place the stored field
/// becomes an <see cref="StarCatalog"/>: both sides go through it, so neither can drift into
/// publishing a differently numbered sky than the other.
/// </para>
/// <para>
/// Earth's guide groups, sky cultures and deep-sky objects are keyed to Earth's own star ids and
/// sky positions, so none of them carry over. They are replaced with empty sets rather than pointed
/// at unrelated stars; the <c>IsGuideStar</c> flag the export already sets is what gives a player
/// anchors to draw from.
/// </para>
/// </remarks>
public static class StarCatalogHandover
{
    /// <summary>Builds the catalog for a stored sky.</summary>
    public static StarCatalog ToCatalog(GalaxySky sky)
    {
        ArgumentNullException.ThrowIfNull(sky);
        return ToCatalog(sky.Placement, sky.StarField);
    }

    /// <summary>Builds the catalog for a stored placement and the field sampled from it.</summary>
    public static StarCatalog ToCatalog(GalaxyPlacement placement, StarField starField)
    {
        ArgumentNullException.ThrowIfNull(placement);
        ArgumentNullException.ThrowIfNull(starField);
        return new StarCatalog(
            StarCatalogExport.BuildEntries(placement, starField)
                .Select(static entry => new StarCatalogEntry(
                    entry.Hip,
                    entry.RightAscensionDeg,
                    entry.DeclinationDeg,
                    entry.VisualMagnitude,
                    entry.BvColorIndex,
                    entry.IsGuideStar))
                .ToList(),
            guideGroups: [],
            skyCultures: [],
            deepSkyObjects: []);
    }
}
