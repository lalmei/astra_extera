using System.Globalization;
using System.Text;

namespace AstraExtera.Galaxy;

/// <summary>
/// Habitable-zone and full-system SVG figures for the static preview page.
/// </summary>
public static class LocalSystemSvg
{
    public static string RenderHabitableZone(GalaxyPlacement placement)
        => Render(placement, zoneView: true);

    public static string RenderFullSystem(GalaxyPlacement placement)
        => Render(placement, zoneView: false);

    private static string Render(GalaxyPlacement placement, bool zoneView)
    {
        ArgumentNullException.ThrowIfNull(placement);
        var system = placement.System;
        var maxAu = LocalSystemGeometry.MaxAu(system, zoneView);
        var cx = LocalSystemGeometry.Cx;
        var cy = LocalSystemGeometry.Cy;
        var uid = zoneView ? "zone" : "system";
        var star = LocalSystemGeometry.StarColors(system.StarClass);
        var starSize = zoneView ? 9.0 : 6.0;
        var asMoon = placement.WorldKind == ObserverWorldKind.TerrestrialMoon;
        var angle = zoneView ? LocalSystemGeometry.ZoneWorldAngleRad : LocalSystemGeometry.SystemWorldAngleRad;
        var orbitR = LocalSystemGeometry.RadiusPx(system.OrbitalDistanceAu, maxAu);
        var world = LocalSystemGeometry.PointOnOrbit(orbitR, angle);
        var innerR = LocalSystemGeometry.RadiusPx(system.HabitableZoneInnerAu, maxAu);
        var outerR = LocalSystemGeometry.RadiusPx(system.HabitableZoneOuterAu, maxAu);
        var label = zoneView ? "Habitable zone of the host star" : "Full system around the host star";

        var svg = new StringBuilder();
        svg.AppendLine($"""<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 {F(LocalSystemGeometry.ViewWidth)} {F(LocalSystemGeometry.ViewHeight)}" role="img" aria-label="{label}">""");
        svg.AppendLine("""<rect width="480" height="300" fill="#0b1020"/>""");
        svg.AppendLine("<defs>");
        svg.AppendLine($"""<radialGradient id="{uid}-star" cx="38%" cy="34%" r="62%"><stop offset="0%" stop-color="{star.Core}"/><stop offset="55%" stop-color="{star.Mid}"/><stop offset="100%" stop-color="{star.Edge}"/></radialGradient>""");
        svg.AppendLine($"""<radialGradient id="{uid}-glow" cx="50%" cy="50%" r="50%"><stop offset="0%" stop-color="{star.Glow}" stop-opacity="0.55"/><stop offset="70%" stop-color="{star.Glow}" stop-opacity="0.12"/><stop offset="100%" stop-color="{star.Glow}" stop-opacity="0"/></radialGradient>""");
        svg.AppendLine("""<radialGradient id="world-fill" cx="32%" cy="30%" r="70%"><stop offset="0%" stop-color="#9ad4ff"/><stop offset="38%" stop-color="#3d8f6e"/><stop offset="100%" stop-color="#0b1c18"/></radialGradient>""");
        svg.AppendLine("""<radialGradient id="giant-fill" cx="30%" cy="28%" r="72%"><stop offset="0%" stop-color="#f3d9a4"/><stop offset="40%" stop-color="#c48a3a"/><stop offset="100%" stop-color="#4a2a12"/></radialGradient>""");
        svg.AppendLine("""<radialGradient id="ice-fill" cx="30%" cy="28%" r="72%"><stop offset="0%" stop-color="#dbeafe"/><stop offset="45%" stop-color="#38bdf8"/><stop offset="100%" stop-color="#0c4a6e"/></radialGradient>""");
        svg.AppendLine("""<radialGradient id="rocky-fill" cx="32%" cy="30%" r="70%"><stop offset="0%" stop-color="#e7c6a0"/><stop offset="55%" stop-color="#a16207"/><stop offset="100%" stop-color="#431407"/></radialGradient>""");
        svg.AppendLine("</defs>");

        if (zoneView)
        {
            svg.AppendLine(Circle(cx, cy, outerR, "#1a3d2a", "none", 0, 0.55));
            svg.AppendLine(Circle(cx, cy, innerR, "#0b1020", "none", 0, 1.0));
        }
        else
        {
            svg.AppendLine(Circle(cx, cy, outerR, "#1a3d2a", "none", 0, 0.35));
        }

        svg.AppendLine(Orbit(cx, cy, innerR, "#ef4444", dashed: true));
        svg.AppendLine(Orbit(cx, cy, outerR, "#60a5fa", dashed: true));
        svg.AppendLine(Orbit(cx, cy, orbitR, "#2a3654", dashed: false));

        if (!zoneView)
        {
            svg.AppendLine(Orbit(cx, cy, LocalSystemGeometry.RadiusPx(system.SnowLineAu, maxAu), "#94a3b8", dotted: true));
        }

        var companionIndex = 0;
        foreach (var body in system.Companions)
        {
            var radius = LocalSystemGeometry.RadiusPx(body.SemiMajorAxisAu, maxAu);
            if (radius > LocalSystemGeometry.MaxRadiusPx * 0.98)
            {
                continue;
            }

            svg.AppendLine(Orbit(cx, cy, radius, "#2a3654", dashed: false));
            var marker = LocalSystemGeometry.PointOnOrbit(radius, 1.2 + companionIndex * 1.4);
            var size = body.Role == CompanionRole.ShepherdGiant ? 10.0
                : body.Role == CompanionRole.OuterIceGiant ? 7.0
                : 4.0;
            var fill = body.Role == CompanionRole.ShepherdGiant ? "url(#giant-fill)"
                : body.Role == CompanionRole.OuterIceGiant ? "url(#ice-fill)"
                : "url(#rocky-fill)";
            svg.AppendLine(Circle(marker.X, marker.Y, size, fill, "none", 0));
            if (!zoneView)
            {
                svg.AppendLine($"""<text x="{F(marker.X)}" y="{F(marker.Y + size + 12)}" text-anchor="middle" fill="#8b97ab" font-size="9">{LocalSystemGeometry.CompanionLabel(body.Role)}</text>""");
            }

            companionIndex++;
        }

        svg.AppendLine(Circle(cx, cy, starSize * 2.8, $"url(#{uid}-glow)", "none", 0));
        svg.AppendLine(Circle(cx, cy, starSize, $"url(#{uid}-star)", "none", 0));

        if (asMoon)
        {
            AppendMoonFamily(svg, system, world, zoneView);
        }
        else
        {
            var worldR = zoneView ? 6.0 : 4.0;
            svg.AppendLine(Circle(world.X, world.Y, worldR + 3.0, "#7ec8ff", "none", 0, 0.18));
            svg.AppendLine(Circle(world.X, world.Y, worldR, "url(#world-fill)", "#fff4e0", 1.2));
            if (zoneView)
            {
                svg.AppendLine($"""<text x="{F(world.X)}" y="{F(world.Y + worldR + 14)}" text-anchor="middle" fill="#86efac" font-size="10">world</text>""");
            }
        }

        svg.AppendLine("</svg>");
        return svg.ToString();
    }

    private static void AppendMoonFamily(
        StringBuilder svg,
        LocalSystem system,
        (double X, double Y) giant,
        bool labeled)
    {
        var moons = system.Moons.Length > 0
            ? system.Moons
            : [new SystemMoon(1, 12, 1, 1, 1, true)];
        var farthest = moons.Max(static moon => moon.OrbitalDistanceEarthRadii);
        var giantR = labeled ? 10.0 : 6.0;
        var moonR = labeled ? 6.0 : 4.0;
        var reach = labeled ? 26.0 : 12.0;

        foreach (var moon in moons)
        {
            var orbit = giantR + 3.0 + moon.OrbitalDistanceEarthRadii / farthest * reach;
            svg.AppendLine(Orbit(giant.X, giant.Y, orbit, "#2a3654", dashed: true));
        }

        svg.AppendLine(Circle(giant.X, giant.Y, giantR, "url(#giant-fill)", "none", 0));

        foreach (var moon in moons)
        {
            var orbit = giantR + 3.0 + moon.OrbitalDistanceEarthRadii / farthest * reach;
            var angle = -0.4 + moon.Index * 0.7;
            var x = giant.X + Math.Cos(angle) * orbit;
            var y = giant.Y + Math.Sin(angle) * orbit;
            if (moon.Habitable)
            {
                svg.AppendLine(Circle(x, y, moonR + 3.0, "#7ec8ff", "none", 0, 0.18));
                svg.AppendLine(Circle(x, y, moonR, "url(#world-fill)", "#fff4e0", 1.2));
                if (labeled)
                {
                    svg.AppendLine($"""<text x="{F(x)}" y="{F(y + moonR + 14)}" text-anchor="middle" fill="#86efac" font-size="10">world</text>""");
                }
            }
            else
            {
                svg.AppendLine(Circle(x, y, labeled ? 2.4 : 1.6, "#94a3b8", "none", 0));
            }
        }
    }

    private static string Orbit(double cx, double cy, double radius, string stroke, bool dashed = false, bool dotted = false)
    {
        var dash = dotted ? "1 6" : dashed ? "3 4" : "1.5 5";
        var width = dashed ? 1.25 : 0.85;
        var opacity = dashed ? 0.85 : 0.55;
        return $"""<circle cx="{F(cx)}" cy="{F(cy)}" r="{F(Math.Max(radius, 0.5))}" fill="none" stroke="{stroke}" stroke-width="{F(width)}" stroke-dasharray="{dash}" opacity="{F(opacity)}"/>""";
    }

    private static string Circle(double cx, double cy, double r, string fill, string stroke, double strokeWidth, double opacity = 1.0)
        => $"""<circle cx="{F(cx)}" cy="{F(cy)}" r="{F(r)}" fill="{fill}" stroke="{stroke}" stroke-width="{F(strokeWidth)}" opacity="{F(opacity)}"/>""";

    private static string F(double value)
        => value.ToString("0.###", CultureInfo.InvariantCulture);
}
