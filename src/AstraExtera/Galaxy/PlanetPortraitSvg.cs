using System.Globalization;
using System.Net;
using System.Text;

namespace AstraExtera.Galaxy;

/// <summary>
/// The portrait strip as SVG for the preview page: every companion drawn as a disc with its bands,
/// its storm, its rings at the tilt they actually run, and its moons.
/// </summary>
/// <remarks>
/// Layout comes from <see cref="PlanetPortraits"/>, which the in-game panel draws from as well, so
/// the two pictures of the same system stay the same picture.
/// </remarks>
public static class PlanetPortraitSvg
{
    public static string Render(GalaxyPlacement placement, LocalSystemSky? localSky = null)
    {
        ArgumentNullException.ThrowIfNull(placement);
        var portraits = PlanetPortraits.Layout(placement.System);
        var names = NamesFrom(localSky);

        var svg = new StringBuilder();
        svg.AppendLine(
            $"""<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 {F(PlanetPortraits.ViewWidth)} {F(PlanetPortraits.ViewHeight)}" role="img" aria-label="The companion planets drawn at portrait size">""");
        svg.AppendLine(
            $"""<rect width="{F(PlanetPortraits.ViewWidth)}" height="{F(PlanetPortraits.ViewHeight)}" fill="#0b1020"/>""");
        svg.AppendLine("<defs>");
        svg.AppendLine("""<radialGradient id="portrait-shade" cx="32%" cy="28%" r="78%"><stop offset="0%" stop-color="#ffffff" stop-opacity="0.22"/><stop offset="55%" stop-color="#000000" stop-opacity="0"/><stop offset="100%" stop-color="#000000" stop-opacity="0.55"/></radialGradient>""");
        svg.AppendLine("</defs>");

        for (var i = 0; i < portraits.Count; i++)
        {
            AppendPortrait(svg, portraits[i], i, names.Count > i ? names[i] : null);
        }

        svg.AppendLine("</svg>");
        return svg.ToString();
    }

    private static void AppendPortrait(StringBuilder svg, PlanetPortrait portrait, int index, string? name)
    {
        var disc = portrait.DiscPx;
        var clipId = $"portrait-clip-{index}";
        svg.AppendLine($"""<clipPath id="{clipId}"><circle cx="0" cy="0" r="{F(disc)}"/></clipPath>""");
        svg.AppendLine($"""<g transform="translate({F(portrait.Cx)} {F(portrait.Cy)})">""");

        AppendMoons(svg, portrait);

        var roll = portrait.RingRollRad * 180.0 / Math.PI;
        svg.AppendLine($"""<g transform="rotate({F(roll)})">""");

        if (portrait.HasRing && portrait.Body.Ring is { } ring)
        {
            AppendRing(svg, portrait, ring, front: false);
        }

        AppendDisc(svg, portrait, clipId);

        if (portrait.HasRing && portrait.Body.Ring is { } front)
        {
            AppendRing(svg, portrait, front, front: true);
        }

        svg.AppendLine("</g>");

        var labelY = disc + PlanetPortraits.RingRise(portrait) + 13.0;
        var title = name ?? portrait.Label;
        svg.AppendLine(Text(0, labelY, title, "#f4f7fb", 10));
        svg.AppendLine(Text(0, labelY + 11.0, portrait.SizeLabel, "#8b97ab", 9));
        svg.AppendLine("</g>");
    }

    private static void AppendDisc(StringBuilder svg, PlanetPortrait portrait, string clipId)
    {
        var disc = portrait.DiscPx;
        if (portrait.Appearance is not { } appearance)
        {
            var (r, g, b) = PlanetPortraits.RockyTint();
            svg.AppendLine($"""<circle cx="0" cy="0" r="{F(disc)}" fill="{PlanetPortraits.Hex(r, g, b)}"/>""");
            svg.AppendLine($"""<circle cx="0" cy="0" r="{F(disc)}" fill="url(#portrait-shade)"/>""");
            return;
        }

        var light = PlanetPortraits.Hex(appearance.BandLightR, appearance.BandLightG, appearance.BandLightB);
        var dark = PlanetPortraits.Hex(appearance.BandDarkR, appearance.BandDarkG, appearance.BandDarkB);
        svg.AppendLine($"""<g clip-path="url(#{clipId})">""");
        svg.AppendLine($"""<circle cx="0" cy="0" r="{F(disc)}" fill="{light}"/>""");
        foreach (var band in PlanetPortraits.Bands(appearance))
        {
            svg.AppendLine(
                $"""<rect x="{F(-disc)}" y="{F(band.Top * disc)}" width="{F(disc * 2.0)}" height="{F((band.Bottom - band.Top) * disc)}" fill="{(band.Light ? light : dark)}"/>""");
        }

        if (appearance.Storm is { } storm)
        {
            var placement = PlanetPortraits.StormPlacement(storm);
            svg.AppendLine(
                $"""<ellipse cx="{F(placement.X * disc)}" cy="{F(placement.Y * disc)}" rx="{F(placement.RadiusX * disc)}" ry="{F(placement.RadiusY * disc)}" fill="{PlanetPortraits.Hex(storm.TintR, storm.TintG, storm.TintB)}" stroke="#00000055" stroke-width="0.4"/>""");
        }

        svg.AppendLine("</g>");
        svg.AppendLine($"""<circle cx="0" cy="0" r="{F(disc)}" fill="url(#portrait-shade)"/>""");
    }

    /// <summary>
    /// Half of the ring ellipse: the far half goes behind the planet, the near half over it, which
    /// is the only cue that says the ring is a disc the planet sits inside rather than a halo.
    /// </summary>
    /// <remarks>
    /// Drawn as a circle inside a squashed group rather than as an ellipse path, so that the band's
    /// thickness is foreshortened with the rest of it. Stroking an ellipse directly would keep an
    /// even width all the way round and turn a nearly edge-on ring into a slab.
    /// </remarks>
    private static void AppendRing(StringBuilder svg, PlanetPortrait portrait, PlanetRing ring, bool front)
    {
        var openness = Math.Max(portrait.RingOpenness, 0.015);
        var color = PlanetPortraits.Hex(ring.TintR, ring.TintG, ring.TintB);
        var opacity = Math.Clamp(0.25 + (ring.OpticalDepth * 0.7), 0.12, 0.95);
        var width = Math.Max(0.6, portrait.RingOuterPx - portrait.RingInnerPx);
        var mid = (portrait.RingOuterPx + portrait.RingInnerPx) * 0.5;

        svg.AppendLine($"""<g transform="scale(1 {F(openness)})">""");
        svg.AppendLine(HalfCircle(mid, front, color, width, opacity));
        if (ring.HasDivision && portrait.RingDivisionPx > 0.0)
        {
            svg.AppendLine(HalfCircle(portrait.RingDivisionPx, front, "#0b1020", Math.Max(0.4, width * 0.18), 0.9));
        }

        svg.AppendLine("</g>");
    }

    private static string HalfCircle(double radius, bool front, string stroke, double width, double opacity)
    {
        var sweep = front ? 1 : 0;
        return
            $"""<path d="M {F(-radius)} 0 A {F(radius)} {F(radius)} 0 0 {sweep} {F(radius)} 0" fill="none" stroke="{stroke}" stroke-width="{F(width)}" opacity="{F(opacity)}"/>""";
    }

    private static void AppendMoons(StringBuilder svg, PlanetPortrait portrait)
    {
        if (portrait.MoonCount == 0)
        {
            return;
        }

        var y = -portrait.DiscPx - PlanetPortraits.RingRise(portrait) - 7.0;
        var span = (portrait.MoonCount - 1) * portrait.MoonSpacingPx;
        for (var i = 0; i < portrait.MoonCount; i++)
        {
            var moon = portrait.Body.Moons[i];
            var x = -span * 0.5 + (i * portrait.MoonSpacingPx);
            var r = Math.Clamp(moon.RadiusEarth * 4.0, 0.9, 2.4);
            svg.AppendLine($"""<circle cx="{F(x)}" cy="{F(y)}" r="{F(r)}" fill="#cbd5e1" opacity="0.85"/>""");
        }
    }

    private static IReadOnlyList<string> NamesFrom(LocalSystemSky? localSky)
        => localSky is null ? [] : localSky.Planets.Select(static planet => planet.DisplayName).ToList();

    private static string Text(double x, double y, string value, string fill, double size)
        => $"""<text x="{F(x)}" y="{F(y)}" text-anchor="middle" fill="{fill}" font-size="{F(size)}">{WebUtility.HtmlEncode(value)}</text>""";

    private static string F(double value)
        => value.ToString("0.###", CultureInfo.InvariantCulture);
}
