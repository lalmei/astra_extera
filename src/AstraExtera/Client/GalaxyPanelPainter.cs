using AstraExtera.Galaxy;
using Cairo;
using Vintagestory.API.Client;

namespace AstraExtera.Client;

/// <summary>
/// Draws the galaxy panel: the same two figures and all-sky view the static debug page renders as
/// SVG and PNG, plus the written facts, composed with Cairo for the in-game dialog.
/// </summary>
/// <remarks>
/// Everything is laid out in the design space below and scaled to whatever the dialog was given, so
/// the panel keeps its proportions at any GUI scale. Figure positions come from
/// <see cref="GalaxyFigureGeometry"/> rather than being restated here.
/// </remarks>
public static class GalaxyPanelPainter
{
    public const double DesignWidth = 1060.0;
    public const double DesignHeight = 780.0;

    private const double FactsX = 14.0;
    private const double FactsY = 6.0;
    private const double FactsWidth = 496.0;
    private const double FactsHeight = 768.0;
    private const double TermWidth = 126.0;

    private const double SkyX = 524.0;
    private const double SkyY = 6.0;
    private const double SkyWidth = 520.0;
    private const double SkyHeight = 260.0;

    private const double FaceX = 524.0;
    private const double FaceY = 278.0;
    private const double FaceSize = 256.0;

    private const double EdgeX = 790.0;
    private const double EdgeY = 278.0;
    private const double EdgeWidth = 256.0;
    private const double EdgeHeight = EdgeWidth * GalaxyFigureGeometry.EdgeViewHeight / GalaxyFigureGeometry.EdgeViewWidth;

    private static readonly double[] Ink = [0.957, 0.969, 0.984, 1.0];
    private static readonly double[] Muted = [0.545, 0.592, 0.671, 1.0];
    private static readonly double[] Gold = [0.831, 0.627, 0.090, 1.0];

    public static void Paint(
        Context ctx,
        ElementBounds bounds,
        GalaxyPlacement placement,
        StarField starField,
        byte[] sky,
        LocalSystemSky? localSky = null)
    {
        ArgumentNullException.ThrowIfNull(ctx);
        ArgumentNullException.ThrowIfNull(bounds);
        ArgumentNullException.ThrowIfNull(placement);
        ArgumentNullException.ThrowIfNull(starField);
        ArgumentNullException.ThrowIfNull(sky);

        ctx.Save();
        ctx.Scale(bounds.InnerWidth / DesignWidth, bounds.InnerHeight / DesignHeight);

        PaintFacts(ctx, placement, starField, localSky);
        PaintSky(ctx, sky);
        PaintFaceOn(ctx, placement);
        PaintEdgeOn(ctx, placement);

        ctx.Restore();
    }

    private static void PaintFacts(
        Context ctx,
        GalaxyPlacement placement,
        StarField starField,
        LocalSystemSky? localSky)
    {
        ctx.Save();
        ctx.Rectangle(FactsX, FactsY, FactsWidth, FactsHeight);
        ctx.Clip();

        var heading = CairoFont.WhiteSmallText().WithFontSize(12f).WithColor(Gold);
        var term = CairoFont.WhiteSmallText().WithFontSize(11f).WithColor(Muted);
        var value = CairoFont.WhiteSmallText().WithFontSize(11f).WithColor(Ink);

        var y = FactsY + 12.0;
        var lede = CairoFont.WhiteSmallText().WithFontSize(11f).WithColor(Muted);
        y += DrawWrapped(ctx, lede, GalaxyFacts.Lede(placement), FactsX, y, FactsWidth, 14.0) + 10.0;

        foreach (var section in GalaxyFacts.Describe(placement, starField, localSky))
        {
            heading.SetupContext(ctx);
            ctx.MoveTo(FactsX, y);
            ctx.ShowText(section.Heading.ToUpperInvariant());
            y += 6.0;

            SetColor(ctx, 0x1c2740);
            ctx.LineWidth = 1.0;
            ctx.MoveTo(FactsX, y);
            ctx.LineTo(FactsX + FactsWidth, y);
            ctx.Stroke();
            y += 14.0;

            foreach (var row in section.Rows)
            {
                term.SetupContext(ctx);
                ctx.MoveTo(FactsX, y);
                ctx.ShowText(row.Term);

                y += DrawWrapped(ctx, value, row.Value, FactsX + TermWidth, y, FactsWidth - TermWidth, 13.0);
                y += 2.0;
            }

            y += 8.0;
        }

        ctx.Restore();
    }

    private static void PaintSky(Context ctx, byte[] rgb)
    {
        const int width = GalaxySkyView.Width;
        const int height = GalaxySkyView.Height;
        var stride = width * 4;
        var data = new byte[stride * height];
        for (var row = 0; row < height; row++)
        {
            var source = row * width * 3;
            var target = row * stride;
            for (var column = 0; column < width; column++)
            {
                // Cairo's RGB24 is a 32-bit little-endian word, so the bytes run blue, green, red.
                data[target] = rgb[source + 2];
                data[target + 1] = rgb[source + 1];
                data[target + 2] = rgb[source];
                data[target + 3] = 255;
                source += 3;
                target += 4;
            }
        }

        using var surface = new ImageSurface(data, Format.Rgb24, width, height, stride);
        ctx.Save();
        ctx.Translate(SkyX, SkyY);
        ctx.Scale(SkyWidth / width, SkyHeight / height);
        ctx.SetSourceSurface(surface, 0, 0);
        ctx.Rectangle(0, 0, width, height);
        ctx.Fill();
        ctx.Restore();

        Frame(ctx, SkyX, SkyY, SkyWidth, SkyHeight);
        Caption(ctx, SkyX + 8.0, SkyY + SkyHeight - 8.0, "All-sky, galactic coordinates");
    }

    private static void PaintFaceOn(Context ctx, GalaxyPlacement placement)
    {
        var galaxy = placement.Galaxy;
        var cx = GalaxyFigureGeometry.FaceCx;
        var cy = GalaxyFigureGeometry.FaceCy;

        ctx.Save();
        ctx.Translate(FaceX, FaceY);
        ctx.Scale(FaceSize / GalaxyFigureGeometry.FaceSize, FaceSize / GalaxyFigureGeometry.FaceSize);
        ctx.Rectangle(0, 0, GalaxyFigureGeometry.FaceSize, GalaxyFigureGeometry.FaceSize);
        ctx.Clip();

        SetColor(ctx, 0x0b1020);
        ctx.Rectangle(0, 0, GalaxyFigureGeometry.FaceSize, GalaxyFigureGeometry.FaceSize);
        ctx.Fill();

        Ring(ctx, cx, cy, GalaxyFigureGeometry.FaceRadius(GalaxyFigureGeometry.DiskRadiusKpc), 0x1c2740, 1.0);

        Disc(ctx, cx, cy, GalaxyFigureGeometry.FaceRadius(galaxy.OuterHabitableRadiusKpc), 0x1a3d2a, 0.35);
        Disc(ctx, cx, cy, GalaxyFigureGeometry.FaceRadius(galaxy.InnerHabitableRadiusKpc), 0x0b1020);
        Ring(ctx, cx, cy, GalaxyFigureGeometry.FaceRadius(galaxy.OuterHabitableRadiusKpc), 0xd4a017, 1.5);
        Ring(ctx, cx, cy, GalaxyFigureGeometry.FaceRadius(galaxy.InnerHabitableRadiusKpc), 0xd4a017, 1.5);

        if (galaxy.IsElliptical)
        {
            foreach (var fraction in GalaxyFigureGeometry.EllipticalIsophoteFractions)
            {
                Ring(
                    ctx,
                    cx,
                    cy,
                    GalaxyFigureGeometry.FaceRadius(galaxy.DiskScaleLengthKpc * fraction),
                    0xc4b48a,
                    1.0,
                    0.35);
            }
        }
        else if (galaxy.Morphology == GalaxyMorphology.BarredSpiral)
        {
            var half = GalaxyFigureGeometry.FaceBarHalfLength(galaxy);
            RoundedRect(ctx, cx - half, cy - 10.0, half * 2.0, 20.0, 8.0);
            SetColor(ctx, 0x6b5a3a, 0.85);
            ctx.Fill();
        }

        Disc(ctx, cx, cy, 7.0, 0xf2e6c2);

        for (var arm = 0; arm < galaxy.SpiralArmCount; arm++)
        {
            var points = GalaxyFigureGeometry.ArmPoints(galaxy, arm);
            ctx.NewPath();
            ctx.MoveTo(points[0].X, points[0].Y);
            for (var i = 1; i < points.Count; i++)
            {
                ctx.LineTo(points[i].X, points[i].Y);
            }

            SetColor(ctx, 0x9ec5ff, 0.85);
            ctx.LineWidth = 2.2;
            ctx.Stroke();
        }

        var observer = GalaxyFigureGeometry.FacePoint(
            placement.Location.GalactocentricRadiusKpc,
            placement.Location.AzimuthRad);

        SetColor(ctx, 0x8ec8ff);
        ctx.LineWidth = 1.0;
        ctx.SetDash([4.0, 4.0], 0.0);
        ctx.MoveTo(cx, cy);
        ctx.LineTo(observer.X, observer.Y);
        ctx.Stroke();
        ctx.SetDash([], 0.0);

        Marker(ctx, observer.X, observer.Y, 6.0);
        ctx.Restore();

        Frame(ctx, FaceX, FaceY, FaceSize, FaceSize);
        Caption(ctx, FaceX + 8.0, FaceY + FaceSize - 8.0, "Face-on");
    }

    private static void PaintEdgeOn(Context ctx, GalaxyPlacement placement)
    {
        var galaxy = placement.Galaxy;
        var padX = GalaxyFigureGeometry.EdgePadX;
        var padY = GalaxyFigureGeometry.EdgePadY;
        var plotWidth = GalaxyFigureGeometry.EdgePlotWidth;
        var plotHeight = GalaxyFigureGeometry.EdgePlotHeight;
        var midY = GalaxyFigureGeometry.EdgeMidY;

        ctx.Save();
        ctx.Translate(EdgeX, EdgeY);
        ctx.Scale(
            EdgeWidth / GalaxyFigureGeometry.EdgeViewWidth,
            EdgeHeight / GalaxyFigureGeometry.EdgeViewHeight);
        ctx.Rectangle(0, 0, GalaxyFigureGeometry.EdgeViewWidth, GalaxyFigureGeometry.EdgeViewHeight);
        ctx.Clip();

        SetColor(ctx, 0x0b1020);
        ctx.Rectangle(0, 0, GalaxyFigureGeometry.EdgeViewWidth, GalaxyFigureGeometry.EdgeViewHeight);
        ctx.Fill();

        SetColor(ctx, 0x121a2e);
        ctx.Rectangle(padX, padY, plotWidth, plotHeight);
        ctx.FillPreserve();
        SetColor(ctx, 0x1c2740);
        ctx.LineWidth = 1.0;
        ctx.Stroke();

        SetColor(ctx, 0x2a3654);
        ctx.MoveTo(padX, midY);
        ctx.LineTo(padX + plotWidth, midY);
        ctx.Stroke();

        var inner = GalaxyFigureGeometry.EdgeX(galaxy.InnerHabitableRadiusKpc);
        var outer = GalaxyFigureGeometry.EdgeX(galaxy.OuterHabitableRadiusKpc);
        var band = GalaxyFigureGeometry.EdgeHabitableHeight(galaxy);
        SetColor(ctx, 0x1a3d2a, 0.55);
        ctx.Rectangle(inner, midY - band / 2.0, outer - inner, band);
        ctx.Fill();

        if (galaxy.IsElliptical)
        {
            var rx = GalaxyFigureGeometry.EdgeX(galaxy.DiskScaleLengthKpc) - padX;
            var ry = galaxy.DiskScaleLengthKpc * galaxy.AxisRatio * 1000.0
                     / GalaxyFigureGeometry.EdgeExtentPc(galaxy) * (plotHeight / 2.0);
            ctx.Save();
            ctx.Translate(padX, midY);
            ctx.Scale(Math.Max(rx, 0.001), Math.Max(ry, 0.001));
            ctx.NewPath();
            ctx.Arc(0, 0, 1.0, 0, 2.0 * Math.PI);
            ctx.Restore();
            SetColor(ctx, 0xc4b48a, 0.5);
            ctx.LineWidth = 1.0;
            ctx.Stroke();
        }

        Marker(
            ctx,
            GalaxyFigureGeometry.EdgeX(placement.Location.GalactocentricRadiusKpc),
            GalaxyFigureGeometry.EdgeY(galaxy, placement.Location.HeightPc),
            5.0);
        ctx.Restore();

        Frame(ctx, EdgeX, EdgeY, EdgeWidth, EdgeHeight);
        Caption(ctx, EdgeX + 8.0, EdgeY + EdgeHeight - 8.0, "Edge-on, R vs z");
    }

    /// <summary>The observer: a red dot with a pale rim, matching the debug page.</summary>
    private static void Marker(Context ctx, double x, double y, double radius)
    {
        ctx.NewPath();
        ctx.Arc(x, y, radius, 0, 2.0 * Math.PI);
        SetColor(ctx, 0xff5a5a);
        ctx.FillPreserve();
        SetColor(ctx, 0xfff4e0);
        ctx.LineWidth = 2.0;
        ctx.Stroke();
    }

    private static void Disc(Context ctx, double x, double y, double radius, uint color, double alpha = 1.0)
    {
        ctx.NewPath();
        ctx.Arc(x, y, Math.Max(radius, 0.0), 0, 2.0 * Math.PI);
        SetColor(ctx, color, alpha);
        ctx.Fill();
    }

    private static void Ring(Context ctx, double x, double y, double radius, uint color, double width, double alpha = 1.0)
    {
        ctx.NewPath();
        ctx.Arc(x, y, Math.Max(radius, 0.0), 0, 2.0 * Math.PI);
        SetColor(ctx, color, alpha);
        ctx.LineWidth = width;
        ctx.Stroke();
    }

    private static void RoundedRect(Context ctx, double x, double y, double width, double height, double radius)
    {
        var r = Math.Min(radius, Math.Min(width, height) / 2.0);
        ctx.NewPath();
        ctx.Arc(x + width - r, y + r, r, -Math.PI / 2.0, 0);
        ctx.Arc(x + width - r, y + height - r, r, 0, Math.PI / 2.0);
        ctx.Arc(x + r, y + height - r, r, Math.PI / 2.0, Math.PI);
        ctx.Arc(x + r, y + r, r, Math.PI, 1.5 * Math.PI);
        ctx.ClosePath();
    }

    private static void Caption(Context ctx, double x, double y, string text)
    {
        var font = CairoFont.WhiteSmallText().WithFontSize(10f).WithColor(Muted);
        font.SetupContext(ctx);
        ctx.MoveTo(x, y);
        ctx.ShowText(text);
    }

    /// <summary>
    /// Writes <paramref name="text"/> at <paramref name="x"/>,<paramref name="y"/> and returns how
    /// far the cursor advanced, including the first line. Long metallicity and disk rows wrap rather
    /// than running into the figures.
    /// </summary>
    private static double DrawWrapped(Context ctx, CairoFont font, string text, double x, double y, double maxWidth, double lineHeight)
    {
        font.SetupContext(ctx);
        var words = text.Split(' ');
        var line = string.Empty;
        var lineY = y;
        foreach (var word in words)
        {
            var candidate = line.Length == 0 ? word : line + " " + word;
            if (line.Length > 0 && ctx.TextExtents(candidate).Width > maxWidth)
            {
                ctx.MoveTo(x, lineY);
                ctx.ShowText(line);
                line = word;
                lineY += lineHeight;
            }
            else
            {
                line = candidate;
            }
        }

        ctx.MoveTo(x, lineY);
        ctx.ShowText(line);
        return lineY - y + lineHeight;
    }

    private static void Frame(Context ctx, double x, double y, double width, double height)
    {
        SetColor(ctx, 0x1c2740);
        ctx.LineWidth = 1.0;
        ctx.Rectangle(x, y, width, height);
        ctx.Stroke();
    }

    private static void SetColor(Context ctx, uint rgb, double alpha = 1.0)
        => ctx.SetSourceRGBA(
            ((rgb >> 16) & 0xFF) / 255.0,
            ((rgb >> 8) & 0xFF) / 255.0,
            (rgb & 0xFF) / 255.0,
            alpha);
}
