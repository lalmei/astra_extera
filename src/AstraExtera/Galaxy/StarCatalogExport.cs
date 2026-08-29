using System.Text.Json;
using System.Text.Json.Serialization;

namespace AstraExtera.Galaxy;

/// <summary>
/// One row of AstraTerra's star catalog. Mirrored rather than referenced because AstraExtera does
/// not link AstraTerra's assembly; the property names must keep matching
/// <c>astraterra:data/star-catalog.v1.json</c>, which AstraTerra reads with web-style camelCase.
/// </summary>
public sealed record AstraTerraStarEntry(
    int Hip,
    double RightAscensionDeg,
    double DeclinationDeg,
    double VisualMagnitude,
    double? BvColorIndex,
    bool IsGuideStar);

public sealed record StarCatalogExportOptions
{
    /// <summary>
    /// Stars flagged for emphasis and naming. Earth's navigators settled on 58; the same handful of
    /// anchors is what a player needs to start drawing their own figures.
    /// </summary>
    public int GuideStarCount { get; init; } = 58;

    public bool Indent { get; init; }
}

/// <summary>
/// Turns a sampled star field into the catalog AstraTerra renders.
/// <para>
/// Catalog ids are assigned by brightness rank, so id 1 is always this world's brightest star.
/// Because a player's constellation is stored as edges between ids, those ids are the save's
/// contract: the server samples the catalog once and stores it, and clients render that list
/// rather than drawing another. Sampler changes therefore do not scramble figures on an existing
/// save. Regenerating the galaxy still follows <see cref="GalaxyPlacement.CurrentSchemaVersion"/>.
/// </para>
/// </summary>
public static class StarCatalogExport
{
    private static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web)
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.Never
    };

    public static IReadOnlyList<AstraTerraStarEntry> BuildEntries(
        GalaxyPlacement placement,
        StarField starField,
        StarCatalogExportOptions? options = null)
    {
        var settings = options ?? new StarCatalogExportOptions();
        var orientation = placement.Orientation;
        var entries = new List<AstraTerraStarEntry>(starField.Stars.Count);

        for (var index = 0; index < starField.Stars.Count; index++)
        {
            var star = starField.Stars[index];
            var (rightAscension, declination) = orientation.ToEquatorial(
                star.GalacticLongitudeRad,
                star.GalacticLatitudeRad);

            // Rounding a value just shy of a full turn can land on 360 exactly; keep it in [0, 360).
            var roundedRightAscension = Math.Round(rightAscension, 5) % 360.0;

            entries.Add(new AstraTerraStarEntry(
                Hip: index + 1,
                RightAscensionDeg: roundedRightAscension,
                DeclinationDeg: Math.Round(declination, 5),
                VisualMagnitude: Math.Round(star.ApparentMagnitude, 3),
                BvColorIndex: Math.Round(star.ColorIndexBv, 3),
                IsGuideStar: index < settings.GuideStarCount));
        }

        return entries;
    }

    public static string ToJson(
        GalaxyPlacement placement,
        StarField starField,
        StarCatalogExportOptions? options = null)
    {
        var settings = options ?? new StarCatalogExportOptions();
        var writerOptions = settings.Indent
            ? new JsonSerializerOptions(Options) { WriteIndented = true }
            : Options;
        return JsonSerializer.Serialize(BuildEntries(placement, starField, settings), writerOptions);
    }

    /// <summary>
    /// Companion <c>guide-stars.v1.json</c>. A procedurally authored sky has no inherited
    /// constellations, so it ships empty on purpose: the named groupings are the players' to make.
    /// </summary>
    public static string EmptyGuideGroupsJson() => "[]";
}
