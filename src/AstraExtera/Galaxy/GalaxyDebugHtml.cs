using System.Net;
using System.Text;

namespace AstraExtera.Galaxy;

public static class GalaxyDebugHtml
{
    public static string Render(GalaxyPlacement placement, StarField? stars = null)
    {
        var starField = stars ?? StarFieldSampler.Sample(placement);
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
        html.AppendLine($"<img id=\"milky-way-sky\" alt=\"Host galaxy on the sky from this world\" src=\"{GalaxySkyView.RenderPngDataUri(placement, starField)}\">");
        html.AppendLine("<p class=\"axis\">longitude −180° · nucleus at 0° · +180° · latitude +90° (top) to −90° (bottom)</p>");
        html.AppendLine("</figure>");
        html.AppendLine("</section>");
        html.AppendLine("<section class=\"facts\">");
        foreach (var section in GalaxyFacts.Describe(placement, starField))
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
        figure { margin: 0; }
        figcaption { margin: 0 0 8px; color: var(--muted); font-size: 13px; }
        svg { display: block; width: 100%; height: auto; border: 1px solid var(--line); }
        .sky { margin: 0 0 32px; }
        .sky img { display: block; width: 100%; height: auto; border: 1px solid var(--line); background: #070b14; }
        .axis { margin: 8px 0 0; color: var(--muted); font-size: 12px; }
        dl { margin: 0; display: grid; gap: 10px; }
        dl div { display: grid; grid-template-columns: 140px minmax(0, 1fr); gap: 12px; padding: 10px 0; border-top: 1px solid var(--line); }
        dt { color: var(--muted); }
        dd { margin: 0; }
        @media (max-width: 840px) {
          .figures { grid-template-columns: 1fr; }
          dl div { grid-template-columns: 1fr; gap: 4px; }
        }
        """;
}
