using AstraExtera.Galaxy;
using Cairo;
using Vintagestory.API.Client;

namespace AstraExtera.Client;

/// <summary>
/// Draws the galaxy panel: the written facts in a scrolling column, and the same all-sky, face-on,
/// edge-on and local-system figures the static debug page renders as PNG and SVG.
/// </summary>
/// <remarks>
/// <para>
/// The facts and the figures are two separate elements, because only the facts scroll. Each is laid
/// out in the design space below and scaled to whatever the dialog gave it, so the panel keeps its
/// proportions at any GUI scale. Figure positions come from <see cref="GalaxyFigureGeometry"/> and
/// <see cref="LocalSystemGeometry"/> rather than being restated here.
/// </para>
/// <para>
/// Text goes through <see cref="ShowRich"/> rather than Cairo's ShowText, so that the solar and
/// Earth symbols in <see cref="GalaxyFacts"/> are drawn as vectors. None of the fonts Vintage Story
/// ships has a glyph for U+2609 or U+2295, so writing them as text would put a box on the panel.
/// </para>
/// </remarks>
public static class GalaxyPanelPainter
{
    public const double FactsWidth = 486.0;
    public const double ScrollbarWidth = 20.0;
    public const double ColumnGap = 14.0;
    public const double FiguresWidth = 520.0;

    /// <summary>Height of the visible facts column, and of the figure column beside it.</summary>
    public const double DesignHeight = 776.0;

    public const double DesignWidth = FactsWidth + ScrollbarWidth + ColumnGap + FiguresWidth;

    private const double TermWidth = 126.0;
    private const double ValueLineHeight = 13.0;
    private const double LedeLineHeight = 14.0;

    // Figure boxes, relative to the figure column's own origin.
    private const double SkyX = 0.0;
    private const double SkyY = 0.0;
    private const double SkyWidth = FiguresWidth;
    private const double SkyHeight = 250.0;

    private const double FaceX = 0.0;
    private const double FaceY = 262.0;
    private const double FaceSize = 252.0;

    private const double EdgeX = 268.0;
    private const double EdgeY = 262.0;
    private const double EdgeWidth = 252.0;
    private const double EdgeHeight = EdgeWidth * GalaxyFigureGeometry.EdgeViewHeight / GalaxyFigureGeometry.EdgeViewWidth;

    // Two scales on purpose: a shepherd giant beyond the snow line sits several times farther out
    // than the liquid-water belt, so one diagram cannot hold both without crushing the inner system.
    // The zone view is fitted to the habitable orbits, the system view to the outermost planet --
    // the same split the preview page and the Historia Extera cosmology panel draw.
    private const double SystemY = 546.0;
    private const double SystemWidth = 252.0;
    private const double SystemHeight = SystemWidth * LocalSystemGeometry.ViewHeight / LocalSystemGeometry.ViewWidth;
    private const double ZoneX = 0.0;
    private const double SystemX = 268.0;

    // The portrait strip takes what is left of the figure column, so the panel keeps its height.
    private const double PortraitX = 0.0;
    private const double PortraitY = SystemY + SystemHeight + 8.0;
    private const double PortraitWidth = FiguresWidth;
    private const double PortraitHeight = DesignHeight - PortraitY;

    private static readonly double[] Ink = [0.957, 0.969, 0.984, 1.0];
    private static readonly double[] Muted = [0.545, 0.592, 0.671, 1.0];
    private static readonly double[] Gold = [0.831, 0.627, 0.090, 1.0];

    private static CairoFont HeadingFont => CairoFont.WhiteSmallText().WithFontSize(12f).WithColor(Gold);

    private static CairoFont TermFont => CairoFont.WhiteSmallText().WithFontSize(11f).WithColor(Muted);

    private static CairoFont ValueFont => CairoFont.WhiteSmallText().WithFontSize(11f).WithColor(Ink);

    private static CairoFont LedeFont => CairoFont.WhiteSmallText().WithFontSize(11f).WithColor(Muted);

    /// <summary>
    /// Draws the facts column, scrolled up by <paramref name="scrollOffset"/> design units. The
    /// caller clips to the element, so only the visible slice lands on the panel.
    /// </summary>
    public static void PaintFacts(
        Context ctx,
        ElementBounds bounds,
        GalaxyPlacement placement,
        StarField starField,
        LocalSystemSky? localSky,
        double scrollOffset)
    {
        ArgumentNullException.ThrowIfNull(ctx);
        ArgumentNullException.ThrowIfNull(bounds);
        ArgumentNullException.ThrowIfNull(placement);
        ArgumentNullException.ThrowIfNull(starField);

        ctx.Save();
        ctx.Rectangle(0, 0, bounds.InnerWidth, bounds.InnerHeight);
        ctx.Clip();

        var scale = bounds.InnerWidth / FactsWidth;
        ctx.Scale(scale, scale);
        ctx.Translate(0, -scrollOffset);

        LayOutFacts(ctx, placement, starField, localSky);

        ctx.Restore();
    }

    /// <summary>
    /// How tall the facts column is in design units, so the dialog can size its scrollbar. Measured
    /// with the same code that draws it, against a throwaway surface.
    /// </summary>
    public static double MeasureFactsHeight(
        GalaxyPlacement placement,
        StarField starField,
        LocalSystemSky? localSky)
    {
        ArgumentNullException.ThrowIfNull(placement);
        ArgumentNullException.ThrowIfNull(starField);

        using var surface = new ImageSurface(Format.Argb32, 1, 1);
        using var ctx = new Context(surface);
        return LayOutFacts(ctx, placement, starField, localSky, measureOnly: true);
    }

    public static void PaintFigures(
        Context ctx,
        ElementBounds bounds,
        GalaxyPlacement placement,
        byte[] sky,
        LocalSystemSky? localSky = null)
    {
        ArgumentNullException.ThrowIfNull(ctx);
        ArgumentNullException.ThrowIfNull(bounds);
        ArgumentNullException.ThrowIfNull(placement);
        ArgumentNullException.ThrowIfNull(sky);

        ctx.Save();
        ctx.Scale(bounds.InnerWidth / FiguresWidth, bounds.InnerHeight / DesignHeight);

        PaintSky(ctx, sky);
        PaintFaceOn(ctx, placement);
        PaintEdgeOn(ctx, placement);
        PaintLocalSystem(ctx, placement, ZoneX, zoneView: true);
        PaintLocalSystem(ctx, placement, SystemX, zoneView: false);
        PaintPortraits(ctx, placement, localSky);

        ctx.Restore();
    }

    /// <summary>
    /// Walks the facts once, drawing unless <paramref name="measureOnly"/>, and returns the height
    /// used. One routine so the measured height and the drawn height cannot drift apart.
    /// </summary>
    private static double LayOutFacts(
        Context ctx,
        GalaxyPlacement placement,
        StarField starField,
        LocalSystemSky? localSky,
        bool measureOnly = false)
    {
        var heading = HeadingFont;
        var term = TermFont;
        var value = ValueFont;
        var lede = LedeFont;

        var y = 12.0;
        y += DrawWrapped(ctx, lede, GalaxyFacts.Lede(placement), 0, y, FactsWidth, LedeLineHeight, measureOnly) + 10.0;

        foreach (var section in GalaxyFacts.Describe(placement, starField, localSky))
        {
            if (!measureOnly)
            {
                heading.SetupContext(ctx);
                ShowRich(ctx, heading, section.Heading.ToUpperInvariant(), 0, y);
            }

            y += 6.0;

            if (!measureOnly)
            {
                SetColor(ctx, 0x1c2740);
                ctx.LineWidth = 1.0;
                ctx.MoveTo(0, y);
                ctx.LineTo(FactsWidth, y);
                ctx.Stroke();
            }

            y += 14.0;

            foreach (var row in section.Rows)
            {
                // The term column wraps too: "Equilibrium temperature" is wider than the gutter, and
                // running it under the value is what put two strings on one line before.
                var termHeight = DrawWrapped(ctx, term, row.Term, 0, y, TermWidth - 6.0, ValueLineHeight, measureOnly);
                var valueHeight = DrawWrapped(ctx, value, row.Value, TermWidth, y, FactsWidth - TermWidth, ValueLineHeight, measureOnly);
                y += Math.Max(termHeight, valueHeight) + 2.0;
            }

            y += 8.0;
        }

        return y;
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

    /// <summary>
    /// The host star and its orbits, the same figure <see cref="LocalSystemSvg"/> draws for the
    /// preview page. Letterboxed inside the box so the orbits stay circular.
    /// </summary>
    private static void PaintLocalSystem(Context ctx, GalaxyPlacement placement, double boxX, bool zoneView)
    {
        var system = placement.System;
        var maxAu = LocalSystemGeometry.MaxAu(system, zoneView);
        var cx = LocalSystemGeometry.Cx;
        var cy = LocalSystemGeometry.Cy;
        var scale = SystemWidth / LocalSystemGeometry.ViewWidth;

        ctx.Save();
        ctx.Rectangle(boxX, SystemY, SystemWidth, SystemHeight);
        ctx.Clip();

        SetColor(ctx, 0x0b1020);
        ctx.Rectangle(boxX, SystemY, SystemWidth, SystemHeight);
        ctx.Fill();

        ctx.Translate(boxX, SystemY);
        ctx.Scale(scale, scale);

        var innerR = LocalSystemGeometry.RadiusPx(system.HabitableZoneInnerAu, maxAu, zoneView);
        var outerR = LocalSystemGeometry.RadiusPx(system.HabitableZoneOuterAu, maxAu, zoneView);
        var orbitR = LocalSystemGeometry.RadiusPx(system.OrbitalDistanceAu, maxAu, zoneView);

        if (zoneView)
        {
            Disc(ctx, cx, cy, outerR, 0x1a3d2a, 0.55);
            Disc(ctx, cx, cy, innerR, 0x0b1020);
        }
        else
        {
            Disc(ctx, cx, cy, outerR, 0x1a3d2a, 0.35);
        }

        Orbit(ctx, cx, cy, innerR, 0xef4444, dashed: true);
        Orbit(ctx, cx, cy, outerR, 0x60a5fa, dashed: true);
        if (!zoneView)
        {
            Orbit(ctx, cx, cy, LocalSystemGeometry.RadiusPx(system.SnowLineAu, maxAu, zoneView), 0x94a3b8, dotted: true);
        }

        Orbit(ctx, cx, cy, orbitR, 0x2a3654);

        var companionIndex = 0;
        for (var index = 0; index < system.Companions.Length; index++)
        {
            var body = system.Companions[index];
            var radius = LocalSystemGeometry.RadiusPx(body.SemiMajorAxisAu, maxAu, zoneView);
            if (radius > LocalSystemGeometry.MaxRadiusPx * 0.98)
            {
                continue;
            }

            Orbit(ctx, cx, cy, radius, 0x2a3654);
            var marker = LocalSystemGeometry.PointOnOrbit(radius, 1.2 + companionIndex * 1.4);
            var size = LocalSystemGeometry.BodyRadiusPx(body.RadiusEarth, zoneView);
            var fill = body.Role switch
            {
                CompanionRole.ShepherdGiant or CompanionRole.OuterGasGiant => 0xc48a3au,
                CompanionRole.OuterIceGiant => 0x38bdf8u,
                _ => 0xa16207u
            };

            // The ring line runs at the angle the rings actually run, matching the portrait below.
            if (body.Appearance is { Ring: not null } ringed)
            {
                var span = size * ringed.Ring!.OuterRadiusPlanetRadii;
                EllipseOutline(
                    ctx,
                    marker.X,
                    marker.Y,
                    span,
                    Math.Max(0.35, span * GiantAppearances.RingOpenness(ringed)),
                    GiantAppearances.RingRollRadians(ringed),
                    PlanetPortraits.Rgb(ringed.Ring.TintR, ringed.Ring.TintG, ringed.Ring.TintB),
                    width: 0.9,
                    alpha: 0.8);
            }

            Disc(ctx, marker.X, marker.Y, size, fill);
            if (!zoneView && LocalSystemGeometry.MapLabel(system.Companions, index) is { } caption)
            {
                CenteredCaption(ctx, marker.X, marker.Y + size + 12.0, caption);
            }

            companionIndex++;
        }

        var starSize = zoneView ? 9.0 : 6.0;
        var starColor = ParseHex(LocalSystemGeometry.StarColors(system.StarClass).Mid);
        Disc(ctx, cx, cy, starSize * 2.8, starColor, 0.22);
        Disc(ctx, cx, cy, starSize, starColor);

        var angle = zoneView ? LocalSystemGeometry.ZoneWorldAngleRad : LocalSystemGeometry.SystemWorldAngleRad;
        var world = LocalSystemGeometry.PointOnOrbit(orbitR, angle);
        var worldR = zoneView ? 6.0 : 4.0;
        if (placement.WorldKind == ObserverWorldKind.TerrestrialMoon)
        {
            PaintMoonFamily(ctx, system, world, zoneView);
        }
        else
        {
            Disc(ctx, world.X, world.Y, worldR + 3.0, 0x7ec8ff, 0.18);
            Disc(ctx, world.X, world.Y, worldR, 0x3d8f6e);
            Ring(ctx, world.X, world.Y, worldR, 0xfff4e0, 1.2);
        }

        ctx.Restore();

        Frame(ctx, boxX, SystemY, SystemWidth, SystemHeight);
        Caption(
            ctx,
            boxX + 8.0,
            SystemY + SystemHeight - 8.0,
            zoneView ? "Liquid-water belt" : "Full system, to the outermost planet");
    }

    private static void PaintMoonFamily(Context ctx, LocalSystem system, (double X, double Y) giant, bool zoneView)
    {
        var moons = system.Moons.Length > 0
            ? system.Moons
            : [new SystemMoon(1, 12, 1, 1, 1, true)];
        var farthest = moons.Max(static moon => moon.OrbitalDistanceEarthRadii);
        var giantR = zoneView ? 10.0 : 6.0;
        var moonR = zoneView ? 6.0 : 4.0;
        var reach = zoneView ? 26.0 : 12.0;

        foreach (var moon in moons)
        {
            var orbit = giantR + 3.0 + moon.OrbitalDistanceEarthRadii / farthest * reach;
            Orbit(ctx, giant.X, giant.Y, orbit, 0x2a3654, dashed: true);
        }

        Disc(ctx, giant.X, giant.Y, giantR, 0xc48a3a);

        foreach (var moon in moons)
        {
            var orbit = giantR + 3.0 + moon.OrbitalDistanceEarthRadii / farthest * reach;
            var angle = -0.4 + moon.Index * 0.7;
            var x = giant.X + Math.Cos(angle) * orbit;
            var y = giant.Y + Math.Sin(angle) * orbit;
            if (moon.Habitable)
            {
                Disc(ctx, x, y, moonR + 3.0, 0x7ec8ff, 0.18);
                Disc(ctx, x, y, moonR, 0x3d8f6e);
                Ring(ctx, x, y, moonR, 0xfff4e0, 1.2);
            }
            else
            {
                Disc(ctx, x, y, zoneView ? 2.4 : 1.6, 0x94a3b8);
            }
        }
    }

    /// <summary>
    /// The portrait strip: every companion at disc size, with its banding, its storm, its rings at
    /// the tilt they run, and its moons. The same layout <see cref="PlanetPortraitSvg"/> draws for
    /// the preview page, scaled into whatever the figure column has left below the system figures.
    /// </summary>
    private static void PaintPortraits(Context ctx, GalaxyPlacement placement, LocalSystemSky? localSky)
    {
        var portraits = PlanetPortraits.Layout(placement.System);
        if (portraits.Count == 0)
        {
            return;
        }

        var names = localSky?.Planets.Select(static planet => planet.DisplayName).ToList() ?? [];

        ctx.Save();
        ctx.Rectangle(PortraitX, PortraitY, PortraitWidth, PortraitHeight);
        ctx.Clip();
        SetColor(ctx, 0x0b1020);
        ctx.Rectangle(PortraitX, PortraitY, PortraitWidth, PortraitHeight);
        ctx.Fill();

        ctx.Translate(PortraitX, PortraitY);
        var scale = Math.Min(
            PortraitWidth / PlanetPortraits.ViewWidth,
            PortraitHeight / PlanetPortraits.ViewHeight);
        ctx.Scale(scale, scale);

        for (var i = 0; i < portraits.Count; i++)
        {
            PaintPortrait(ctx, portraits[i], i < names.Count ? names[i] : null);
        }

        ctx.Restore();

        Frame(ctx, PortraitX, PortraitY, PortraitWidth, PortraitHeight);
    }

    private static void PaintPortrait(Context ctx, PlanetPortrait portrait, string? name)
    {
        var cx = portrait.Cx;
        var cy = portrait.Cy;
        var disc = portrait.DiscPx;
        var ring = portrait.Body.Ring;

        PaintPortraitMoons(ctx, portrait);

        if (ring is not null && portrait.HasRing)
        {
            PaintPortraitRing(ctx, portrait, ring, front: false);
        }

        PaintPortraitDisc(ctx, portrait);

        if (ring is not null && portrait.HasRing)
        {
            PaintPortraitRing(ctx, portrait, ring, front: true);
        }

        var labelY = cy + disc + PlanetPortraits.RingRise(portrait) + 13.0;
        CenteredCaption(ctx, cx, labelY, name ?? portrait.Label);
        CenteredCaption(ctx, cx, labelY + 11.0, portrait.SizeLabel);
    }

    private static void PaintPortraitDisc(Context ctx, PlanetPortrait portrait)
    {
        var disc = portrait.DiscPx;
        if (portrait.Appearance is not { } appearance)
        {
            var (r, g, b) = PlanetPortraits.RockyTint();
            Disc(ctx, portrait.Cx, portrait.Cy, disc, PlanetPortraits.Rgb(r, g, b));
            ShadeDisc(ctx, portrait);
            return;
        }

        var light = PlanetPortraits.Rgb(appearance.BandLightR, appearance.BandLightG, appearance.BandLightB);
        var dark = PlanetPortraits.Rgb(appearance.BandDarkR, appearance.BandDarkG, appearance.BandDarkB);

        ctx.Save();
        ctx.Translate(portrait.Cx, portrait.Cy);
        ctx.Rotate(portrait.RingRollRad);
        ctx.NewPath();
        ctx.Arc(0, 0, disc, 0, 2.0 * Math.PI);
        ctx.Clip();

        SetColor(ctx, light);
        ctx.Rectangle(-disc, -disc, disc * 2.0, disc * 2.0);
        ctx.Fill();

        foreach (var band in PlanetPortraits.Bands(appearance))
        {
            SetColor(ctx, band.Light ? light : dark);
            ctx.Rectangle(-disc, band.Top * disc, disc * 2.0, (band.Bottom - band.Top) * disc);
            ctx.Fill();
        }

        if (appearance.Storm is { } storm)
        {
            var spot = PlanetPortraits.StormPlacement(storm);
            ctx.Save();
            ctx.Translate(spot.X * disc, spot.Y * disc);
            ctx.Scale(1.0, Math.Max(0.05, spot.RadiusY / spot.RadiusX));
            ctx.NewPath();
            ctx.Arc(0, 0, spot.RadiusX * disc, 0, 2.0 * Math.PI);
            ctx.Restore();
            SetColor(ctx, PlanetPortraits.Rgb(storm.TintR, storm.TintG, storm.TintB));
            ctx.Fill();
        }

        ctx.Restore();
        ShadeDisc(ctx, portrait);
    }

    /// <summary>A dark limb on the shaded side, so a disc reads as a globe rather than a coin.</summary>
    private static void ShadeDisc(Context ctx, PlanetPortrait portrait)
    {
        var disc = portrait.DiscPx;
        using var shade = new RadialGradient(
            portrait.Cx - disc * 0.35,
            portrait.Cy - disc * 0.40,
            disc * 0.10,
            portrait.Cx,
            portrait.Cy,
            disc);
        shade.AddColorStop(0.0, new Color(1.0, 1.0, 1.0, 0.20));
        shade.AddColorStop(0.55, new Color(0.0, 0.0, 0.0, 0.0));
        shade.AddColorStop(1.0, new Color(0.0, 0.0, 0.0, 0.55));

        ctx.NewPath();
        ctx.Arc(portrait.Cx, portrait.Cy, disc, 0, 2.0 * Math.PI);
        ctx.SetSource(shade);
        ctx.Fill();
    }

    private static void PaintPortraitRing(Context ctx, PlanetPortrait portrait, PlanetRing ring, bool front)
    {
        var openness = Math.Max(portrait.RingOpenness, 0.015);
        var color = PlanetPortraits.Rgb(ring.TintR, ring.TintG, ring.TintB);
        var alpha = Math.Clamp(0.25 + (ring.OpticalDepth * 0.7), 0.12, 0.95);
        var width = Math.Max(0.6, portrait.RingOuterPx - portrait.RingInnerPx);
        var mid = (portrait.RingOuterPx + portrait.RingInnerPx) * 0.5;

        RingBand(ctx, portrait, mid, openness, front, color, width, alpha);

        if (ring.HasDivision && portrait.RingDivisionPx > 0.0)
        {
            RingBand(
                ctx,
                portrait,
                portrait.RingDivisionPx,
                openness,
                front,
                0x0b1020,
                Math.Max(0.4, width * 0.18),
                0.9);
        }
    }

    /// <summary>
    /// One half of a ring band: the far half behind the planet, the near half over it, which is the
    /// only cue that says the ring is a disc the planet sits inside rather than a halo.
    /// </summary>
    /// <remarks>
    /// Stroked under the squashed transform on purpose, so the band's thickness is foreshortened
    /// along with its radius. A stroke of even width would turn a nearly edge-on ring into a slab.
    /// </remarks>
    private static void RingBand(
        Context ctx,
        PlanetPortrait portrait,
        double radius,
        double openness,
        bool front,
        uint color,
        double width,
        double alpha)
    {
        ctx.Save();
        ctx.Translate(portrait.Cx, portrait.Cy);
        ctx.Rotate(portrait.RingRollRad);
        ctx.Scale(1.0, openness);
        ctx.NewPath();
        ctx.Arc(0, 0, radius, front ? 0.0 : Math.PI, front ? Math.PI : 2.0 * Math.PI);
        SetColor(ctx, color, alpha);
        ctx.LineWidth = width;
        ctx.Stroke();
        ctx.Restore();
    }

    /// <summary>
    /// An ellipse outline of even width: the ring line drawn on the system map, where a ring is a
    /// single hairline rather than a band.
    /// </summary>
    private static void EllipseOutline(
        Context ctx,
        double cx,
        double cy,
        double rx,
        double ry,
        double rollRad,
        uint color,
        double width,
        double alpha)
    {
        // The path is built under a squashed transform and stroked under the plain one, so the
        // ellipse is an ellipse but its stroke keeps an even width.
        ctx.Save();
        ctx.Translate(cx, cy);
        ctx.Rotate(rollRad);
        ctx.Scale(1.0, Math.Max(ry / Math.Max(rx, 1e-6), 1e-3));
        ctx.NewPath();
        ctx.Arc(0, 0, rx, 0, 2.0 * Math.PI);
        ctx.Restore();

        SetColor(ctx, color, alpha);
        ctx.LineWidth = width;
        ctx.Stroke();
    }

    private static void PaintPortraitMoons(Context ctx, PlanetPortrait portrait)
    {
        if (portrait.MoonCount == 0)
        {
            return;
        }

        var y = portrait.Cy - portrait.DiscPx - PlanetPortraits.RingRise(portrait) - 7.0;
        var span = (portrait.MoonCount - 1) * portrait.MoonSpacingPx;
        for (var i = 0; i < portrait.MoonCount; i++)
        {
            var moon = portrait.Body.Moons[i];
            var x = portrait.Cx - (span * 0.5) + (i * portrait.MoonSpacingPx);
            Disc(ctx, x, y, Math.Clamp(moon.RadiusEarth * 4.0, 0.9, 2.4), 0xcbd5e1, 0.85);
        }
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

    private static void Orbit(Context ctx, double cx, double cy, double radius, uint color, bool dashed = false, bool dotted = false)
    {
        ctx.NewPath();
        ctx.Arc(cx, cy, Math.Max(radius, 0.5), 0, 2.0 * Math.PI);
        SetColor(ctx, color, dashed ? 0.85 : 0.55);
        ctx.LineWidth = dashed ? 1.25 : 0.85;
        ctx.SetDash(dotted ? [1.0, 6.0] : dashed ? [3.0, 4.0] : [1.5, 5.0], 0.0);
        ctx.Stroke();
        ctx.SetDash([], 0.0);
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
        ShowRich(ctx, font, text, x, y);
    }

    private static void CenteredCaption(Context ctx, double centerX, double y, string text)
    {
        var font = CairoFont.WhiteSmallText().WithFontSize(9f).WithColor(Muted);
        font.SetupContext(ctx);
        ShowRich(ctx, font, text, centerX - MeasureRich(ctx, font, text) / 2.0, y);
    }

    /// <summary>
    /// Writes <paramref name="text"/> at <paramref name="x"/>,<paramref name="y"/> and returns how
    /// far the cursor advanced, including the first line. Long metallicity and disk rows wrap rather
    /// than running into the figures.
    /// </summary>
    private static double DrawWrapped(
        Context ctx,
        CairoFont font,
        string text,
        double x,
        double y,
        double maxWidth,
        double lineHeight,
        bool measureOnly = false)
    {
        font.SetupContext(ctx);
        var words = text.Split(' ');
        var line = string.Empty;
        var lineY = y;
        foreach (var word in words)
        {
            var candidate = line.Length == 0 ? word : line + " " + word;
            if (line.Length > 0 && MeasureRich(ctx, font, candidate) > maxWidth)
            {
                if (!measureOnly)
                {
                    ShowRich(ctx, font, line, x, lineY);
                }

                line = word;
                lineY += lineHeight;
            }
            else
            {
                line = candidate;
            }
        }

        if (!measureOnly)
        {
            ShowRich(ctx, font, line, x, lineY);
        }

        return lineY - y + lineHeight;
    }

    /// <summary>
    /// Draws text in which <see cref="GalaxyFacts.Sun"/> and <see cref="GalaxyFacts.Earth"/> become
    /// vector glyphs, and returns the width used. The shipped fonts have no glyph for either, so a
    /// plain ShowText would put a missing-glyph box where the unit should be.
    /// </summary>
    private static double ShowRich(Context ctx, CairoFont font, string text, double x, double y)
        => WalkRich(ctx, font, text, x, y, draw: true);

    private static double MeasureRich(Context ctx, CairoFont font, string text)
        => WalkRich(ctx, font, text, 0, 0, draw: false);

    private static double WalkRich(Context ctx, CairoFont font, string text, double x, double y, bool draw)
    {
        var size = font.UnscaledFontsize;
        var penX = x;
        var run = 0;

        for (var i = 0; i <= text.Length; i++)
        {
            var atEnd = i == text.Length;
            var isSymbol = !atEnd && (text[i] == '☉' || text[i] == '⊕');
            if (!atEnd && !isSymbol)
            {
                continue;
            }

            if (i > run)
            {
                var chunk = text[run..i];
                if (draw)
                {
                    ctx.MoveTo(penX, y);
                    ctx.ShowText(chunk);
                }

                penX += ctx.TextExtents(chunk).XAdvance;
            }

            if (isSymbol)
            {
                penX += DrawSymbol(ctx, text[i], penX, y, size, draw);
                run = i + 1;
            }
        }

        return penX - x;
    }

    /// <summary>
    /// The Sun as a circle with a centre dot, the Earth as a circle crossed by its axes -- the
    /// standard astronomical forms, drawn to sit on the text baseline at the font's own size.
    /// </summary>
    private static double DrawSymbol(Context ctx, char symbol, double x, double y, double fontSize, bool draw)
    {
        var radius = fontSize * 0.30;
        var advance = radius * 2.0 + fontSize * 0.14;
        if (!draw)
        {
            return advance;
        }

        var cx = x + radius + fontSize * 0.05;
        var cy = y - radius * 1.05;
        var line = Math.Max(0.7, fontSize * 0.07);

        ctx.NewPath();
        ctx.Arc(cx, cy, radius, 0, 2.0 * Math.PI);
        ctx.LineWidth = line;
        ctx.Stroke();

        if (symbol == '☉')
        {
            ctx.NewPath();
            ctx.Arc(cx, cy, radius * 0.30, 0, 2.0 * Math.PI);
            ctx.Fill();
        }
        else
        {
            ctx.NewPath();
            ctx.MoveTo(cx - radius, cy);
            ctx.LineTo(cx + radius, cy);
            ctx.MoveTo(cx, cy - radius);
            ctx.LineTo(cx, cy + radius);
            ctx.LineWidth = line;
            ctx.Stroke();
        }

        return advance;
    }

    private static uint ParseHex(string color)
        => uint.TryParse(color.TrimStart('#'), System.Globalization.NumberStyles.HexNumber, System.Globalization.CultureInfo.InvariantCulture, out var value)
            ? value
            : 0xf2e6c2u;

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
