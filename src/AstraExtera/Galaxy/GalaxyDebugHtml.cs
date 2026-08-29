using System.Net;
using System.Text;

namespace AstraExtera.Galaxy;

public static class GalaxyDebugHtml
{
    public static string Render(GalaxyPlacement placement, StarField? stars = null, LocalSystemSky? localSky = null)
    {
        var starField = stars ?? StarFieldSampler.Sample(placement);
        var wanderers = localSky ?? LocalSystemSky.Author(placement);
        var title = GalaxyFacts.Title(placement);
        var lede = GalaxyFacts.Lede(placement);

        var html = new StringBuilder();
        html.AppendLine("<!DOCTYPE html>");
        html.AppendLine("<html lang=\"en\">");
        html.AppendLine("<head>");
        html.AppendLine("<meta charset=\"utf-8\">");
        html.AppendLine("<meta name=\"viewport\" content=\"width=device-width, initial-scale=1\">");
        html.AppendLine($"<title>{Escape(title)}</title>");
        html.AppendLine("<style>");
        html.AppendLine(PageCss);
        html.AppendLine("</style>");
        html.AppendLine("</head>");
        html.AppendLine("<body>");
        html.AppendLine("<main>");
        html.AppendLine($"<header><p class=\"eyebrow\">AstraExtera debug</p><h1>{Escape(title)}</h1>");
        html.AppendLine($"<p class=\"lede\">{Escape(lede)}</p></header>");
        html.AppendLine("<section class=\"figures\">");
        html.AppendLine("<figure><figcaption>Face-on</figcaption>");
        html.AppendLine(GalaxyDebugSvg.RenderFaceOn(placement));
        html.AppendLine("</figure>");
        html.AppendLine("<figure><figcaption>Edge-on · R vs z, ±1.2 kpc full height</figcaption>");
        html.AppendLine(GalaxyDebugSvg.RenderEdgeOn(placement));
        html.AppendLine("</figure>");
        html.AppendLine("</section>");
        html.AppendLine("<section class=\"sky\">");
        html.AppendLine($"<figure><figcaption>{Escape(GalaxySkyView.Caption(placement))}</figcaption>");
        html.AppendLine("<div class=\"sky-stack\">");
        var glow = GalaxySkyView.RenderGlowRgb(placement);
        html.AppendLine($"<img id=\"milky-way-glow\" alt=\"Unresolved host-galaxy glow\" src=\"data:image/png;base64,{Convert.ToBase64String(RgbPng.Encode(GalaxySkyView.Width, GalaxySkyView.Height, glow))}\">");
        html.AppendLine($"<img id=\"milky-way-stars\" alt=\"Resolved stars\" src=\"{GalaxySkyView.RenderStarOverlayPngDataUri(starField)}\">");
        html.AppendLine("</div>");
        html.AppendLine("<p class=\"axis\">longitude -180deg · nucleus at 0deg · +180deg · latitude +90deg (top) to -90deg (bottom). Glow is the background; stars sit on top.</p>");
        html.AppendLine("</figure>");
        html.AppendLine("<figure><figcaption>Glow broken into an equatorial cubemap for the skybox</figcaption>");
        html.AppendLine("<div class=\"cubemap\">");
        var equatorial = GalaxySkyView.ReprojectToEquatorial(glow, placement.Orientation);
        var faces = SkyCubemap.FromEquirectangular(equatorial, GalaxySkyView.Width, GalaxySkyView.Height);
        for (var face = 0; face < faces.Count; face++)
        {
            html.AppendLine("<div>");
            html.AppendLine($"<img id=\"{SkyCubemap.FaceHtmlId((SkyCubeFace)face)}\" alt=\"{Escape(SkyCubemap.FaceLabel((SkyCubeFace)face))}\" src=\"{GalaxySkyView.RenderCubemapFacePngDataUri(faces[face], SkyCubemap.FaceSize)}\">");
            html.AppendLine($"<p>{Escape(SkyCubemap.FaceLabel((SkyCubeFace)face))}</p>");
            html.AppendLine("</div>");
        }

        html.AppendLine("</div>");
        html.AppendLine("<p class=\"axis\">Six cube faces sampled from the equatorial glow. +Z is this world's celestial north pole.</p>");
        html.AppendLine("</figure>");
        html.AppendLine("</section>");
        html.AppendLine("<section class=\"system\">");
        html.AppendLine("<figure><figcaption>Habitable zone</figcaption>");
        html.AppendLine(LocalSystemSvg.RenderHabitableZone(placement));
        html.AppendLine("<p class=\"axis\">Fitted to the liquid-water belt. The marked body is this save's world.</p>");
        html.AppendLine("</figure>");
        html.AppendLine("<figure><figcaption>Full system</figcaption>");
        html.AppendLine(LocalSystemSvg.RenderFullSystem(placement));
        html.AppendLine("<p class=\"axis\">Fitted to the outermost planet, with distance compressed so the inner orbits stay readable. Body sizes follow radius, compressed the same way.</p>");
        html.AppendLine("</figure>");
        html.AppendLine("</section>");
        html.AppendLine("<section class=\"portraits\">");
        html.AppendLine("<figure><figcaption>Companion planets</figcaption>");
        html.AppendLine(PlanetPortraitSvg.Render(placement, wanderers));
        html.AppendLine("<p class=\"axis\">Discs are to scale against each other. Rings are drawn at the tilt and heading they run, moons above each planet, and a giant's long-lived storm on the band it is caught in.</p>");
        html.AppendLine("</figure>");
        html.AppendLine("</section>");
        html.AppendLine("<section class=\"facts\">");
        foreach (var section in GalaxyFacts.Describe(placement, starField, wanderers))
        {
            html.AppendLine($"<h2>{Escape(section.Heading)}</h2>");
            html.AppendLine("<dl>");
            foreach (var row in section.Rows)
            {
                html.AppendLine(Row(row.Term, row.Value));
            }

            html.AppendLine("</dl>");
        }

        html.AppendLine("</section>");
        html.AppendLine("</main>");
        html.AppendLine("</body>");
        html.AppendLine("</html>");
        return html.ToString();
    }

    private static string Row(string term, string value)
        => $"<div><dt>{Escape(term)}</dt><dd>{Escape(value)}</dd></div>";

    private static string Escape(string value) => WebUtility.HtmlEncode(value);

    private const string PageCss = """
        :root { color-scheme: dark; --bg:#0b1020; --panel:#121a2e; --ink:#f4f7fb; --muted:#8b97ab; --line:#1c2740; --gold:#d4a017; }
        * { box-sizing: border-box; }
        body { margin: 0; background: var(--bg); color: var(--ink); font: 15px/1.45 ui-sans-serif, system-ui, sans-serif; }
        main { max-width: 1080px; margin: 0 auto; padding: 32px 24px 64px; }
        .eyebrow { margin: 0 0 8px; color: var(--gold); letter-spacing: 0.08em; text-transform: uppercase; font-size: 12px; }
        h1 { margin: 0 0 12px; font-size: 28px; font-weight: 600; }
        h2 { margin: 28px 0 12px; font-size: 13px; letter-spacing: 0.08em; text-transform: uppercase; color: var(--muted); }
        .lede { margin: 0; max-width: 62ch; color: var(--muted); }
        .figures { display: grid; grid-template-columns: minmax(0, 1.2fr) minmax(0, 0.8fr); gap: 24px; margin: 32px 0; align-items: start; }
        .system { display: grid; grid-template-columns: 1fr 1fr; gap: 24px; margin: 0 0 32px; align-items: start; }
        figure { margin: 0; }
        figcaption { margin: 0 0 8px; color: var(--muted); font-size: 13px; }
        svg { display: block; width: 100%; height: auto; border: 1px solid var(--line); }
        .portraits { margin: 0 0 32px; }
        .sky { margin: 0 0 32px; }
        .sky-stack { position: relative; border: 1px solid var(--line); background: #070b14; }
        .sky-stack img { display: block; width: 100%; height: auto; border: 0; }
        .sky-stack #milky-way-stars { position: absolute; inset: 0; mix-blend-mode: screen; pointer-events: none; }
        .cubemap { display: grid; grid-template-columns: repeat(6, minmax(0, 1fr)); gap: 8px; }
        .cubemap img { display: block; width: 100%; height: auto; border: 1px solid var(--line); background: #070b14; }
        .cubemap p { margin: 6px 0 0; color: var(--muted); font-size: 11px; }
        .axis { margin: 8px 0 0; color: var(--muted); font-size: 12px; }
        dl { margin: 0; display: grid; gap: 10px; }
        dl div { display: grid; grid-template-columns: 140px minmax(0, 1fr); gap: 12px; padding: 10px 0; border-top: 1px solid var(--line); }
        dt { color: var(--muted); }
        dd { margin: 0; }
        @media (max-width: 840px) {
          .figures { grid-template-columns: 1fr; }
          .system { grid-template-columns: 1fr; }
          .cubemap { grid-template-columns: repeat(3, minmax(0, 1fr)); }
          dl div { grid-template-columns: 1fr; gap: 4px; }
        }
        """;
}
