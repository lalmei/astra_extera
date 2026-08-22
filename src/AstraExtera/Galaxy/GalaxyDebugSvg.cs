using System.Globalization;
using System.Text;

namespace AstraExtera.Galaxy;

/// <summary>
/// Face-on and edge-on SVG figures used by the static galaxy preview page.
/// </summary>
public static class GalaxyDebugSvg
{
    public const double DiskRadiusKpc = 16.0;
    private const double FaceCx = 280.0;
    private const double FaceCy = 280.0;
    private const double FaceScale = 15.5;
    private const double EdgeWidth = 400.0;
    private const double EdgeHeight = 240.0;
    private const double EdgePadX = 16.0;
    private const double EdgePadY = 20.0;

    public static string RenderFaceOn(GalaxyPlacement placement)
    {
        var galaxy = placement.Galaxy;
        var svg = new StringBuilder();
        svg.AppendLine("""<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 560 560" role="img" aria-label="Face-on host galaxy">""");
        svg.AppendLine("""<rect width="560" height="560" fill="#0b1020"/>""");
        svg.AppendLine(Circle(FaceCx, FaceCy, DiskRadiusKpc * FaceScale, "none", "#1c2740", 1.0));
        svg.AppendLine(Circle(FaceCx, FaceCy, galaxy.OuterHabitableRadiusKpc * FaceScale, "#1a3d2a", "none", 0, 0.35));
        svg.AppendLine(Circle(FaceCx, FaceCy, galaxy.InnerHabitableRadiusKpc * FaceScale, "#0b1020", "none", 0, 1.0));
        svg.AppendLine(Circle(FaceCx, FaceCy, galaxy.OuterHabitableRadiusKpc * FaceScale, "none", "#d4a017", 1.5));
        svg.AppendLine(Circle(FaceCx, FaceCy, galaxy.InnerHabitableRadiusKpc * FaceScale, "none", "#d4a017", 1.5));

        if (galaxy.IsElliptical)
        {
            foreach (var fraction in new[] { 0.4, 0.7, 1.0, 1.4, 1.9 })
            {
                svg.AppendLine(Circle(
                    FaceCx,
                    FaceCy,
                    galaxy.DiskScaleLengthKpc * fraction * FaceScale,
                    "none",
                    "#c4b48a",
                    1.0,
                    0.35));
            }
        }
        else if (galaxy.Morphology == GalaxyMorphology.BarredSpiral)
        {
            var barHalf = galaxy.InnerHabitableRadiusKpc * 0.55 * FaceScale;
            svg.AppendLine($"""<rect x="{F(FaceCx - barHalf)}" y="{F(FaceCy - 10)}" width="{F(barHalf * 2)}" height="20" rx="8" fill="#6b5a3a" opacity="0.85"/>""");
        }

        svg.AppendLine(Circle(FaceCx, FaceCy, 7, "#f2e6c2", "none", 0));

        for (var arm = 0; arm < galaxy.SpiralArmCount; arm++)
        {
            svg.AppendLine(ArmPath(galaxy, arm));
        }

        var observer = FacePoint(placement.Location.GalactocentricRadiusKpc, placement.Location.AzimuthRad);
        svg.AppendLine($"""<line x1="{F(FaceCx)}" y1="{F(FaceCy)}" x2="{F(observer.X)}" y2="{F(observer.Y)}" stroke="#8ec8ff" stroke-width="1" stroke-dasharray="4 4"/>""");
        svg.AppendLine($"""<circle id="observer-face" cx="{F(observer.X)}" cy="{F(observer.Y)}" r="6" fill="#ff5a5a" stroke="#fff4e0" stroke-width="2"/>""");
        svg.AppendLine("</svg>");
        return svg.ToString();
    }

    public static string RenderEdgeOn(GalaxyPlacement placement)
    {
        var galaxy = placement.Galaxy;
        var location = placement.Location;
        var zExtentPc = galaxy.IsElliptical
            ? Math.Max(1600.0, galaxy.OuterHabitableRadiusKpc * galaxy.AxisRatio * 1000.0 * 1.2)
            : 1200.0;
        var midY = EdgePadY + EdgeHeight / 2.0;
        var plotWidth = EdgeWidth - EdgePadX * 2;
        var xOfR = (double r) => EdgePadX + (r / DiskRadiusKpc) * plotWidth;
        var yOfZ = (double zPc) => midY - (zPc / zExtentPc) * (EdgeHeight / 2.0);

        var svg = new StringBuilder();
        svg.AppendLine("""<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 400 280" role="img" aria-label="Edge-on radius versus height">""");
        svg.AppendLine("""<rect width="400" height="280" fill="#0b1020"/>""");
        svg.AppendLine($"""<rect x="{F(EdgePadX)}" y="{F(EdgePadY)}" width="{F(plotWidth)}" height="{F(EdgeHeight)}" fill="#121a2e" stroke="#1c2740"/>""");
        svg.AppendLine($"""<line x1="{F(EdgePadX)}" y1="{F(midY)}" x2="{F(EdgePadX + plotWidth)}" y2="{F(midY)}" stroke="#2a3654"/>""");

        var inner = xOfR(galaxy.InnerHabitableRadiusKpc);
        var outer = xOfR(galaxy.OuterHabitableRadiusKpc);
        var ghzHeightPc = galaxy.IsElliptical
            ? galaxy.OuterHabitableRadiusKpc * galaxy.AxisRatio * 1000.0
            : 3.0 * galaxy.ThinDiskScaleHeightPc;
        var ghzHeight = Math.Min(EdgeHeight - 16, ghzHeightPc / zExtentPc * EdgeHeight);
        svg.AppendLine($"""<rect x="{F(inner)}" y="{F(midY - ghzHeight / 2.0)}" width="{F(outer - inner)}" height="{F(ghzHeight)}" fill="#1a3d2a" opacity="0.55"/>""");

        if (galaxy.IsElliptical)
        {
            var rx = xOfR(galaxy.DiskScaleLengthKpc) - EdgePadX;
            var ry = (galaxy.DiskScaleLengthKpc * galaxy.AxisRatio * 1000.0) / zExtentPc * (EdgeHeight / 2.0);
            svg.AppendLine($"""<ellipse cx="{F(EdgePadX)}" cy="{F(midY)}" rx="{F(rx)}" ry="{F(ry)}" fill="none" stroke="#c4b48a" opacity="0.5"/>""");
        }

        svg.AppendLine($"""<circle id="observer-edge" cx="{F(xOfR(location.GalactocentricRadiusKpc))}" cy="{F(yOfZ(location.HeightPc))}" r="5" fill="#ff5a5a" stroke="#fff4e0" stroke-width="2"/>""");
        svg.AppendLine("</svg>");
        return svg.ToString();
    }

    private static string ArmPath(GalaxyBlueprint galaxy, int arm)
    {
        var points = new StringBuilder();
        for (var i = 0; i <= 80; i++)
        {
            var radius = 1.2 + (DiskRadiusKpc - 1.2) * (i / 80.0);
            var angle = GalaxyGenerator.SpiralArmAngleRad(galaxy, arm, radius);
            var point = FacePoint(radius, angle);
            points.Append(i == 0 ? "M" : "L");
            points.Append(F(point.X));
            points.Append(' ');
            points.Append(F(point.Y));
            points.Append(' ');
        }

        return $"""<path d="{points}" fill="none" stroke="#9ec5ff" stroke-width="2.2" opacity="0.85"/>""";
    }

    private static (double X, double Y) FacePoint(double radiusKpc, double azimuthRad)
        => (
            FaceCx + radiusKpc * Math.Cos(azimuthRad) * FaceScale,
            FaceCy - radiusKpc * Math.Sin(azimuthRad) * FaceScale);

    private static string Circle(double cx, double cy, double r, string fill, string stroke, double strokeWidth, double opacity = 1.0)
        => $"""<circle cx="{F(cx)}" cy="{F(cy)}" r="{F(r)}" fill="{fill}" stroke="{stroke}" stroke-width="{F(strokeWidth)}" opacity="{F(opacity)}"/>""";

    private static string F(double value)
        => value.ToString("0.###", CultureInfo.InvariantCulture);
}
