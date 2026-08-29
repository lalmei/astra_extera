using AstraExtera.Client;
using AstraExtera.Galaxy;
using AstraTerra.Astronomy;

namespace AstraExtera.Sync;

/// <summary>
/// Turns this world's near bodies into the catalog AstraTerra draws: the parent giant a moon world
/// hangs beneath, and its sibling moons.
/// </summary>
/// <remarks>
/// A world that is itself a moon has no moon of its own, so the catalog asks for Vintage Story's to
/// stand down. An ordinary planet world keeps it and gets an empty catalog, which is also what
/// undoes the suppression when a player leaves a moon world for a planet one in the same session.
/// </remarks>
public static class NearBodyExport
{
    public static NearBodyCatalog Build(GalaxyPlacement placement)
    {
        ArgumentNullException.ThrowIfNull(placement);
        var bodies = NearSky.Author(placement);
        if (bodies.Count == 0)
        {
            return NearBodyCatalog.Empty;
        }

        var appearance = placement.System.ParentGiantAppearance;
        var moonsByIndex = placement.System.Moons.ToDictionary(static moon => moon.Index);
        var entries = new List<NearBodyEntry>(bodies.Count);

        foreach (var body in bodies)
        {
            var face = body.Role == NearBodyRole.ParentGiant
                ? BodyFacePainter.PaintGiant(appearance, body.DiscFraction)
                : BodyFacePainter.PaintMoon(moonsByIndex[body.SourceIndex], placement.WorldSeed);

            entries.Add(new NearBodyEntry(
                body.Id,
                body.DisplayName,
                body.Role == NearBodyRole.ParentGiant ? NearBodyKind.ParentPlanet : NearBodyKind.Moon,
                body.AngularDiameterDeg,
                body.HourAngleDeg,
                body.HourAngleRateDegPerDay,
                body.DeclinationDeg,
                body.Brightness,
                face));
        }

        return new NearBodyCatalog(NearBodyCatalog.CurrentSchemaVersion, HidesVanillaMoon: true, entries);
    }
}
