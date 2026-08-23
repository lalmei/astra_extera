using System.Globalization;
using System.Text;

namespace AstraExtera.Galaxy;

/// <summary>
/// Face-on and edge-on SVG figures used by the static galaxy preview page. The in-game panel draws
/// the same two figures with Cairo; both take their positions from <see cref="GalaxyFigureGeometry"/>.
/// </summary>
public static class GalaxyDebugSvg
{
    public const double DiskRadiusKpc = GalaxyFigureGeometry.DiskRadiusKpc;

    public static string RenderFaceOn(GalaxyPlacement placement)
    {
        ArgumentNullException.ThrowIfNull(placement);
        var galaxy = placement.Galaxy;
        var cx = GalaxyFigureGeometry.FaceCx;
        var cy = GalaxyFigureGeometry.FaceCy;

        var svg = new StringBuilder();
        svg.AppendLine("""<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 560 560" role="img" aria-label="Face-on host galaxy">""");
        svg.AppendLine("""<rect width="560" height="560" fill="#0b1020"/>""");
        svg.AppendLine(Circle(cx, cy, GalaxyFigureGeometry.FaceRadius(DiskRadiusKpc), "none", "#1c2740", 1.0));
        svg.AppendLine(Circle(cx, cy, GalaxyFigureGeometry.FaceRadius(galaxy.OuterHabitableRadiusKpc), "#1a3d2a", "none", 0, 0.35));
        svg.AppendLine(Circle(cx, cy, GalaxyFigureGeometry.FaceRadius(galaxy.InnerHabitableRadiusKpc), "#0b1020", "none", 0, 1.0));
        svg.AppendLine(Circle(cx, cy, GalaxyFigureGeometry.FaceRadius(galaxy.OuterHabitableRadiusKpc), "none", "#d4a017", 1.5));
        svg.AppendLine(Circle(cx, cy, GalaxyFigureGeometry.FaceRadius(galaxy.InnerHabitableRadiusKpc), "none", "#d4a017", 1.5));

        if (galaxy.IsElliptical)
        {
            foreach (var fraction in GalaxyFigureGeometry.EllipticalIsophoteFractions)
            {
                svg.AppendLine(Circle(
                    cx,
                    cy,
                    GalaxyFigureGeometry.FaceRadius(galaxy.DiskScaleLengthKpc * fraction),
                    "none",
                    "#c4b48a",
                    1.0,
                    0.35));
            }
        }
        else if (galaxy.Morphology == GalaxyMorphology.BarredSpiral)
        {
            var barHalf = GalaxyFigureGeometry.FaceBarHalfLength(galaxy);
            svg.AppendLine($"""<rect x="{F(cx - barHalf)}" y="{F(cy - 10)}" width="{F(barHalf * 2)}" height="20" rx="8" fill="#6b5a3a" opacity="0.85"/>""");
        }

        svg.AppendLine(Circle(cx, cy, 7, "#f2e6c2", "none", 0));

        for (var arm = 0; arm < galaxy.SpiralArmCount; arm++)
        {
            svg.AppendLine(ArmPath(galaxy, arm));
        }

        var observer = GalaxyFigureGeometry.FacePoint(
            placement.Location.GalactocentricRadiusKpc,
            placement.Location.AzimuthRad);
        svg.AppendLine($"""<line x1="{F(cx)}" y1="{F(cy)}" x2="{F(observer.X)}" y2="{F(observer.Y)}" stroke="#8ec8ff" stroke-width="1" stroke-dasharray="4 4"/>""");
        svg.AppendLine($"""<circle id="observer-face" cx="{F(observer.X)}" cy="{F(observer.Y)}" r="6" fill="#ff5a5a" stroke="#fff4e0" stroke-width="2"/>""");
        svg.AppendLine("</svg>");
        return svg.ToString();
    }

    public static string RenderEdgeOn(GalaxyPlacement placement)
    {
        ArgumentNullException.ThrowIfNull(placement);
        var galaxy = placement.Galaxy;
        var location = placement.Location;
        var midY = GalaxyFigureGeometry.EdgeMidY;
        var padX = GalaxyFigureGeometry.EdgePadX;
        var padY = GalaxyFigureGeometry.EdgePadY;
        var plotWidth = GalaxyFigureGeometry.EdgePlotWidth;
        var plotHeight = GalaxyFigureGeometry.EdgePlotHeight;

        var svg = new StringBuilder();
        svg.AppendLine("""<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 400 280" role="img" aria-label="Edge-on radius versus height">""");
        svg.AppendLine("""<rect width="400" height="280" fill="#0b1020"/>""");
        svg.AppendLine($"""<rect x="{F(padX)}" y="{F(padY)}" width="{F(plotWidth)}" height="{F(plotHeight)}" fill="#121a2e" stroke="#1c2740"/>""");
        svg.AppendLine($"""<line x1="{F(padX)}" y1="{F(midY)}" x2="{F(padX + plotWidth)}" y2="{F(midY)}" stroke="#2a3654"/>""");

        var inner = GalaxyFigureGeometry.EdgeX(galaxy.InnerHabitableRadiusKpc);
        var outer = GalaxyFigureGeometry.EdgeX(galaxy.OuterHabitableRadiusKpc);
        var ghzHeight = GalaxyFigureGeometry.EdgeHabitableHeight(galaxy);
        svg.AppendLine($"""<rect x="{F(inner)}" y="{F(midY - ghzHeight / 2.0)}" width="{F(outer - inner)}" height="{F(ghzHeight)}" fill="#1a3d2a" opacity="0.55"/>""");

        if (galaxy.IsElliptical)
        {
            var rx = GalaxyFigureGeometry.EdgeX(galaxy.DiskScaleLengthKpc) - padX;
            var ry = galaxy.DiskScaleLengthKpc * galaxy.AxisRatio * 1000.0
                     / GalaxyFigureGeometry.EdgeExtentPc(galaxy) * (plotHeight / 2.0);
            svg.AppendLine($"""<ellipse cx="{F(padX)}" cy="{F(midY)}" rx="{F(rx)}" ry="{F(ry)}" fill="none" stroke="#c4b48a" opacity="0.5"/>""");
        }

        svg.AppendLine($"""<circle id="observer-edge" cx="{F(GalaxyFigureGeometry.EdgeX(location.GalactocentricRadiusKpc))}" cy="{F(GalaxyFigureGeometry.EdgeY(galaxy, location.HeightPc))}" r="5" fill="#ff5a5a" stroke="#fff4e0" stroke-width="2"/>""");
        svg.AppendLine("</svg>");
        return svg.ToString();
    }

    private static string ArmPath(GalaxyBlueprint galaxy, int arm)
    {
        var points = new StringBuilder();
        var traced = GalaxyFigureGeometry.ArmPoints(galaxy, arm);
        for (var i = 0; i < traced.Count; i++)
        {
            points.Append(i == 0 ? "M" : "L");
            points.Append(F(traced[i].X));
            points.Append(' ');
            points.Append(F(traced[i].Y));
            points.Append(' ');
        }

        return $"""<path d="{points}" fill="none" stroke="#9ec5ff" stroke-width="2.2" opacity="0.85"/>""";
    }

    private static string Circle(double cx, double cy, double r, string fill, string stroke, double strokeWidth, double opacity = 1.0)
        => $"""<circle cx="{F(cx)}" cy="{F(cy)}" r="{F(r)}" fill="{fill}" stroke="{stroke}" stroke-width="{F(strokeWidth)}" opacity="{F(opacity)}"/>""";

    private static string F(double value)
        => value.ToString("0.###", CultureInfo.InvariantCulture);
}
