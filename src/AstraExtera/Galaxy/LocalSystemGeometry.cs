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

    public static double RadiusPx(double au, double maxAu)
        => maxAu <= 0.0 ? 0.0 : au / maxAu * MaxRadiusPx;

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
            _ => "#94a3b8"
        };

    public static string CompanionLabel(CompanionRole role)
        => role switch
        {
            CompanionRole.InnerRocky => "inner rocky",
            CompanionRole.ShepherdGiant => "shepherd",
            CompanionRole.OuterIceGiant => "ice giant",
            _ => role.ToString()
        };
}
