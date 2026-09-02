using AstraExtera.Client;
using AstraExtera.Galaxy;
using AstraTerra.Astronomy;

namespace AstraExtera.Sync;

/// <summary>
/// Turns this world's near bodies into the catalog AstraTerra draws: the parent giant a moon world
/// hangs beneath and its sibling moons, or, on a planet world, that planet's own moons.
/// </summary>
/// <remarks>
/// Either way the catalog asks Vintage Story's moon to stand down, because either way this world is
/// not Earth. A moon world has no moon of its own; a planet world has the moons the generator gave
/// it, which are not that one, and some worlds have none at all. Only the drawing stops: moonlight,
/// the phase the calendar reports, and the length of the day are Vintage Story's own.
/// </remarks>
public static class NearBodyExport
{
    public static NearBodyCatalog Build(GalaxyPlacement placement, CelestialTextureLibrary textures)
    {
        ArgumentNullException.ThrowIfNull(placement);
        ArgumentNullException.ThrowIfNull(textures);
        var bodies = NearSky.Author(placement);
        var appearance = placement.System.ParentGiantAppearance;
        var moonsByIndex = (placement.WorldKind == ObserverWorldKind.TerrestrialMoon
                ? placement.System.Moons
                : placement.System.HomeMoons)
            .ToDictionary(static moon => moon.Index);
        var entries = new List<NearBodyEntry>(bodies.Count);

        foreach (var body in bodies)
        {
            var face = body.Role == NearBodyRole.ParentGiant
                ? BodyFacePainter.PaintGiant(textures, appearance, body.RingOpenness, placement.WorldSeed)
                : BodyFacePainter.PaintMoon(textures, moonsByIndex[body.SourceIndex], placement.WorldSeed);

            entries.Add(new NearBodyEntry(
                body.Id,
                body.DisplayName,
                body.Role == NearBodyRole.ParentGiant ? NearBodyKind.ParentPlanet : NearBodyKind.Moon,
                body.AngularDiameterDeg,
                body.HourAngleDeg,
                body.HourAngleRateDegPerDay,
                body.DeclinationDeg,
                body.Brightness,
                face,
                body.Orbit is { } orbit
                    ? new NearBodyOrbit(
                        orbit.AnchorHourAngleDeg,
                        orbit.DistanceRatio,
                        orbit.PhaseDeg,
                        orbit.PhaseRateDegPerDay)
                    : null));
        }

        return new NearBodyCatalog(NearBodyCatalog.CurrentSchemaVersion, HidesVanillaMoon: true, entries);
    }
}
