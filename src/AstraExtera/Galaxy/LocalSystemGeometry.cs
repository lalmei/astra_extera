namespace AstraExtera.Galaxy;

/// <summary>
/// Shared layout for the habitable-zone and full-system figures. The HTML preview draws them as
/// SVG; holding the scale here keeps both views on the same AU-to-pixel map.
/// </summary>
public static class LocalSystemGeometry
{
    public const double ViewWidth = 480.0;
    public const double ViewHeight = 300.0;
    public const double Cx = ViewWidth / 2.0;
    public const double Cy = ViewHeight / 2.0;
    public const double MaxRadiusPx = 122.0;

    public const double ZoneWorldAngleRad = -0.55;
    public const double SystemWorldAngleRad = -0.35;

    public static double MaxAu(LocalSystem system, bool zoneView)
    {
        ArgumentNullException.ThrowIfNull(system);
        if (zoneView)
        {
            return system.HabitableZoneOuterAu * 1.45;
        }

        var farthest = Math.Max(system.HabitableZoneOuterAu, system.OrbitalDistanceAu);
        farthest = Math.Max(farthest, system.SnowLineAu);
        foreach (var body in system.Companions)
        {
            farthest = Math.Max(farthest, body.SemiMajorAxisAu);
        }

        return farthest * 1.12;
    }

    /// <summary>
    /// How hard the full-system view compresses distance. A system with ice giants beyond a second
    /// gas giant runs a hundred times the width of its liquid-water belt, and a linear map would
    /// collapse every inner orbit onto the star. The zone view stays linear, because there the
    /// point is to read the belt itself.
    /// </summary>
    public const double SystemScaleExponent = 0.55;

    public static double RadiusPx(double au, double maxAu)
        => maxAu <= 0.0 ? 0.0 : au / maxAu * MaxRadiusPx;

    public static double RadiusPx(double au, double maxAu, bool zoneView)
    {
        if (maxAu <= 0.0 || au <= 0.0)
        {
            return 0.0;
        }

        return zoneView
            ? RadiusPx(au, maxAu)
            : Math.Pow(au / maxAu, SystemScaleExponent) * MaxRadiusPx;
    }

    /// <summary>
    /// The drawn radius of a body, in figure pixels. Real radii span two orders of magnitude
    /// between a small rocky world and a gas giant, so the map is compressive: a giant reads as
    /// clearly bigger without a Mercury analog vanishing.
    /// </summary>
    public static double BodyRadiusPx(double radiusEarth, bool zoneView)
    {
        var px = 3.0 * Math.Pow(Math.Max(0.05, radiusEarth), 0.42);
        return Math.Clamp(px, 2.0, 11.0) * (zoneView ? 1.25 : 1.0);
    }

    public static (double X, double Y) PointOnOrbit(double radiusPx, double angleRad)
        => (Cx + Math.Cos(angleRad) * radiusPx, Cy + Math.Sin(angleRad) * radiusPx);

    public static (string Core, string Mid, string Edge, string Glow) StarColors(StarSpectralClass starClass)
        => starClass switch
        {
            StarSpectralClass.M => ("#ffd0a8", "#ff6b35", "#8a1c0a", "#ff5a1f"),
            StarSpectralClass.K => ("#fff1c8", "#ff9f43", "#b45309", "#f59e0b"),
            StarSpectralClass.G => ("#fffce8", "#ffd166", "#ca8a04", "#facc15"),
            StarSpectralClass.F => ("#ffffff", "#fff3c4", "#fde68a", "#fef08a"),
            _ => ("#fffce8", "#ffd166", "#ca8a04", "#facc15")
        };

    public static string CompanionColor(CompanionRole role)
        => role switch
        {
            CompanionRole.InnerRocky => "#a16207",
            CompanionRole.ShepherdGiant => "#c48a3a",
            CompanionRole.OuterIceGiant => "#38bdf8",
            CompanionRole.OuterGasGiant => "#e2b06a",
            _ => "#94a3b8"
        };

    /// <summary>
    /// The label for a body on the system map, or null when the same kind was already labelled. A
    /// system can hold three rocky worlds inside the belt, and three copies of one caption stacked
    /// on top of each other say less than one that counts them.
    /// </summary>
    public static string? MapLabel(CompanionPlanet[] companions, int index)
    {
        ArgumentNullException.ThrowIfNull(companions);
        var role = companions[index].Role;
        var first = Array.FindIndex(companions, body => body.Role == role);
        if (first != index)
        {
            return null;
        }

        var count = companions.Count(body => body.Role == role);
        return count > 1 ? $"{CompanionLabel(role)} x{count}" : CompanionLabel(role);
    }

    public static string CompanionLabel(CompanionRole role)
        => role switch
        {
            CompanionRole.InnerRocky => "inner rocky",
            CompanionRole.ShepherdGiant => "shepherd",
            CompanionRole.OuterIceGiant => "ice giant",
            CompanionRole.OuterGasGiant => "gas giant",
            _ => role.ToString()
        };
}
