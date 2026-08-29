using AstraExtera.Galaxy;
using AstraTerra.Astronomy;

namespace AstraExtera.Sync;

/// <summary>
/// Turns the stored local-system sky into the catalogs AstraTerra renders.
/// </summary>
public static class LocalSystemSkyExport
{
    public static PlanetCatalog ToPlanetCatalog(LocalSystemSky sky)
    {
        ArgumentNullException.ThrowIfNull(sky);
        return new PlanetCatalog(
            LocalSystemSky.SchemaVersion,
            ToElements(sky.Observer),
            sky.Planets.Select(ToPlanet).ToList());
    }

    public static CometCatalog ToCometCatalog(LocalSystemSky sky)
    {
        ArgumentNullException.ThrowIfNull(sky);
        return new CometCatalog(
            LocalSystemSky.SchemaVersion,
            sky.Comets.Select(ToComet).ToList());
    }

    public static IReadOnlyList<MeteorShowerEntry> ToMeteorShowers(LocalSystemSky sky)
    {
        ArgumentNullException.ThrowIfNull(sky);
        return sky.Showers.Select(static shower => new MeteorShowerEntry(
            shower.Id,
            shower.DisplayName,
            shower.RightAscensionDeg,
            shower.DeclinationDeg,
            shower.PeakSolarLongitudeDeg,
            shower.WindowHalfWidthDeg,
            shower.PeakZenithHourlyRate)).ToList();
    }

    private static PlanetEntry ToPlanet(AuthoredPlanet planet)
        => new(
            planet.Id,
            planet.DisplayName,
            ToElements(planet.Orbit),
            planet.AbsoluteMagnitude,
            planet.PhaseCoefficient,
            planet.TintR,
            planet.TintG,
            planet.TintB);

    private static CometEntry ToComet(AuthoredComet comet)
        => new(
            comet.Id,
            comet.DisplayName,
            comet.PeriodYears,
            comet.FirstPerihelionYear,
            comet.WindowHalfWidthYears,
            comet.PeakMagnitude,
            comet.EdgeMagnitude,
            comet.BrighteningExponent,
            comet.PeakTailLengthDeg,
            comet.Path.Select(static keyframe => new CometPathKeyframe(
                keyframe.Phase,
                keyframe.RightAscensionDeg,
                keyframe.DeclinationDeg)).ToList(),
            comet.TintR,
            comet.TintG,
            comet.TintB);

    private static KeplerianElements ToElements(AuthoredOrbit orbit)
        => new(
            orbit.SemiMajorAxisAu,
            0.0,
            orbit.Eccentricity,
            0.0,
            orbit.InclinationDeg,
            0.0,
            orbit.MeanLongitudeDeg,
            orbit.MeanLongitudeRateDegPerCentury,
            orbit.LongitudeOfPerihelionDeg,
            0.0,
            orbit.LongitudeOfAscendingNodeDeg,
            0.0);
}
