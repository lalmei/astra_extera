using System.Globalization;
using System.Net;
using System.Text;

namespace AstraExtera.Galaxy;

public static class GalaxyDebugHtml
{
    public static string Render(GalaxyPlacement placement, StarField? stars = null)
    {
        var starField = stars ?? StarFieldSampler.Sample(placement);
        var galaxy = placement.Galaxy;
        var location = placement.Location;
        var morphology = galaxy.MorphologyLabel;
        var kind = placement.WorldKind == ObserverWorldKind.TerrestrialMoon ? "terrestrial moon" : "terrestrial planet";
        var title = $"AstraExtera galaxy preview - seed {placement.WorldSeed}";
        var lede = galaxy.IsElliptical
            ? "Giant elliptical: no disk or arms. The gold ring is a spherical habitable shell outside the dense core; the red mark is this save's observer."
            : "Face-on disk and edge-on height for the server-authored galactic site. The gold ring is the habitable annulus; the red mark is this save's observer.";

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
        html.AppendLine("<h2>Galaxy</h2>");
        html.AppendLine("<dl>");
        if (galaxy.IsElliptical)
        {
            html.AppendLine(Row("Morphology", $"{morphology}, Sérsic n {F(galaxy.SersicIndex)}, q {F(galaxy.AxisRatio)}"));
            html.AppendLine(Row("Stellar mass", $"{galaxy.StellarMassSolar:0.00e+0} M☉"));
            html.AppendLine(Row("Spheroid", $"Re {F(galaxy.DiskScaleLengthKpc)} kpc"));
        }
        else
        {
            html.AppendLine(Row("Morphology", $"{morphology}, {galaxy.SpiralArmCount} arms, pitch {F(galaxy.SpiralPitchDeg)}°"));
            html.AppendLine(Row("Stellar mass", $"{galaxy.StellarMassSolar:0.00e+0} M☉"));
            html.AppendLine(Row("Disk", $"Rd {F(galaxy.DiskScaleLengthKpc)} kpc, thin-disk h {F(galaxy.ThinDiskScaleHeightPc)} pc, B/D {F(galaxy.BulgeToDiskMass)}"));
        }
        html.AppendLine(Row("Habitable zone", $"{F(galaxy.InnerHabitableRadiusKpc)}–{F(galaxy.OuterHabitableRadiusKpc)} kpc"));
        html.AppendLine(Row("Metallicity", $"gradient {F(galaxy.MetallicityGradientDexPerKpc)} dex/kpc, [Fe/H] at {F(galaxy.MetallicityReferenceRadiusKpc)} kpc = {F(galaxy.SolarAnalogMetallicityFeH)}, scatter {F(galaxy.MetallicityScatterDex)} dex"));
        html.AppendLine("</dl>");
        html.AppendLine("<h2>Observer</h2>");
        html.AppendLine("<dl>");
        html.AppendLine(Row("World", kind));
        html.AppendLine(Row("Location", $"R {F(location.GalactocentricRadiusKpc)} kpc, θ {F(location.AzimuthRad * 180.0 / Math.PI)}°, z {F(location.HeightPc)} pc"));
        html.AppendLine(Row("[Fe/H]", $"{location.MetallicityFeH:+0.00;-0.00}"));
        html.AppendLine(Row("Iron / ores", $"{YesNo(placement.CanHostIronCore)} / {YesNo(placement.CanHostOres)}"));
        html.AppendLine(Row("Spiral arm", galaxy.IsElliptical ? "none" : location.InSpiralArm ? "inside an arm" : "interarm"));
        html.AppendLine(Row("Local density", $"ρ/ρ☉ {F(location.LocalStellarDensityRelativeToSolar)}, SN/SN☉ {F(location.SupernovaRateRelativeToSolar)}"));
        html.AppendLine("</dl>");
        html.AppendLine("<h2>Earth analog</h2>");
        html.AppendLine("<dl>");
        html.AppendLine(Row("Radius", $"{F(placement.World.RadiusEarth)} R⊕"));
        html.AppendLine(Row("Mass", $"{F(placement.World.MassEarth)} M⊕"));
        html.AppendLine(Row("Surface gravity", $"{F(placement.World.SurfaceGravityG)} g"));
        html.AppendLine(Row("Bulk iron", $"{F(placement.World.BulkIronMassFraction * 100.0)} wt%  (Earth 32.1)"));
        html.AppendLine(Row("Core mass", $"{F(placement.World.CoreMassFraction * 100.0)} %  (Earth 32.5)"));
        html.AppendLine(Row("Mean density", $"{F(placement.World.MeanDensityEarth)} ρ⊕"));
        html.AppendLine(Row("Surface temperature", $"{F(placement.World.SurfaceTemperatureK)} K  (placeholder climate; may change)"));
        html.AppendLine(Row("Equilibrium temperature", $"{F(placement.World.EquilibriumTemperatureK)} K"));
        html.AppendLine("</dl>");
        html.AppendLine("<h2>Visible sky</h2>");
        html.AppendLine("<dl>");
        html.AppendLine(Row("Limiting magnitude", $"{F(starField.LimitingMagnitude)} (dark-adapted naked eye)"));
        html.AppendLine(Row(
            "Effective limit",
            $"{F(starField.EffectiveLimitingMagnitude)}{(starField.Truncated ? " (render budget reached first)" : string.Empty)}"));
        html.AppendLine(Row("Naked-eye stars", $"{starField.ExpectedVisibleCount:N0} expected, {starField.SampledCount:N0} drawn"));
        html.AppendLine(Row("Resolved / rendered", $"{starField.Stars.Count:N0}{(starField.Truncated ? " (budget capped)" : string.Empty)}"));
        html.AppendLine(Row(
            "Celestial pole",
            $"{F(placement.Orientation.PoleTiltFromGalacticPoleDeg)}° from the galactic pole (Earth 62.9°)"));
        if (starField.Stars.Count > 0)
        {
            var brightest = starField.Stars[0];
            html.AppendLine(Row(
                "Brightest star",
                $"m {F(brightest.ApparentMagnitude)}, M {F(brightest.AbsoluteMagnitude)}, {F(brightest.DistancePc)} pc, A_V {F(brightest.ExtinctionMagnitudes)}"));
            html.AppendLine(Row("Median distance", $"{F(MedianDistancePc(starField))} pc"));
        }

        html.AppendLine("</dl>");
        html.AppendLine("</section>");
        html.AppendLine("</main>");
        html.AppendLine("</body>");
        html.AppendLine("</html>");
        return html.ToString();
    }

    private static string Row(string term, string value)
        => $"<div><dt>{Escape(term)}</dt><dd>{Escape(value)}</dd></div>";

    private static double MedianDistancePc(StarField starField)
    {
        var distances = starField.Stars.Select(static star => star.DistancePc).OrderBy(static d => d).ToArray();
        return distances[distances.Length / 2];
    }

    private static string YesNo(bool value) => value ? "yes" : "no";

    private static string F(double value)
        => value.ToString("0.###", CultureInfo.InvariantCulture);

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
