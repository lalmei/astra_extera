using AstraExtera.Galaxy;
using Xunit;

namespace AstraExtera.Tests.Galaxy;

public sealed class GalaxySkyViewTests
{
    [Fact]
    public void The_Debug_Page_Is_A_Static_Html_Document()
    {
        var placement = GalaxyGenerator.Generate(42);
        var html = GalaxyDebugHtml.Render(placement);

        Assert.StartsWith("<!DOCTYPE html>", html, StringComparison.Ordinal);
        Assert.Contains("AstraExtera galaxy preview - seed 42", html, StringComparison.Ordinal);
        Assert.Contains("id=\"observer-face\"", html, StringComparison.Ordinal);
        Assert.Contains("id=\"observer-edge\"", html, StringComparison.Ordinal);
        Assert.Contains("Habitable zone", html, StringComparison.Ordinal);
        Assert.Contains("Surface gravity", html, StringComparison.Ordinal);
        Assert.Contains("Bulk iron", html, StringComparison.Ordinal);
        Assert.Contains("id=\"milky-way-glow\"", html, StringComparison.Ordinal);
        Assert.Contains("id=\"milky-way-stars\"", html, StringComparison.Ordinal);
        Assert.Contains("id=\"sky-cube-px\"", html, StringComparison.Ordinal);
        Assert.Contains("id=\"sky-cube-pz\"", html, StringComparison.Ordinal);
        Assert.Contains("mix-blend-mode: screen", html, StringComparison.Ordinal);
        Assert.Contains("data:image/png;base64,", html, StringComparison.Ordinal);
        Assert.DoesNotContain("<script", html, StringComparison.Ordinal);
    }

    [Fact]
    public void A_Disk_World_Sees_A_Bright_Milky_Way_Band()
    {
        var placement = GalaxyGenerator.Generate(42);
        Assert.False(placement.Galaxy.IsElliptical);
        var rgb = GalaxySkyView.RenderRgb(placement);
        Assert.True(MeanBrightness(rgb, row: GalaxySkyView.Height / 2) > MeanBrightness(rgb, row: 8));
    }

    /// <summary>
    /// The disk has to be allowed to fade to a faint halo. A density floor instead reads as a
    /// uniform haze that washes the whole panorama to mid-grey.
    /// </summary>
    [Fact]
    public void The_Sky_Is_Dark_Away_From_The_Galactic_Plane()
    {
        var placement = GalaxyGenerator.Generate(42);
        var rgb = GalaxySkyView.RenderRgb(placement);

        var band = MeanBrightness(rgb, row: GalaxySkyView.Height / 2) - GalaxySkyView.BackgroundSum;
        var pole = MeanBrightness(rgb, row: 2) - GalaxySkyView.BackgroundSum;

        Assert.True(pole < 12.0, $"polar sky glowed at {pole:0.0} above the night background");
        Assert.True(band > 10.0 * pole, $"band {band:0.0} was not clearly brighter than pole {pole:0.0}");
    }

    private static double MeanBrightness(byte[] rgb, int row)
    {
        var sum = 0.0;
        var offset = row * GalaxySkyView.Width * 3;
        for (var x = 0; x < GalaxySkyView.Width; x++)
        {
            var i = offset + x * 3;
            sum += rgb[i] + rgb[i + 1] + rgb[i + 2];
        }

        return sum / GalaxySkyView.Width;
    }

    [Fact]
    public void The_Star_Overlay_Is_Black_Away_From_Stars()
    {
        var placement = GalaxyGenerator.Generate(42);
        var overlay = GalaxySkyView.RenderStarOverlayRgb(StarFieldSampler.Sample(placement));

        Assert.Equal(0, overlay[0] + overlay[1] + overlay[2]);
        Assert.Contains(overlay, static channel => channel > 0);
    }

    [Fact]
    public void The_Equatorial_Glow_Puts_The_Nucleus_Where_The_Orientation_Says()
    {
        var placement = GalaxyGenerator.Generate(42);
        var galactic = GalaxySkyView.RenderGlowRgb(placement);
        var equatorial = GalaxySkyView.ReprojectToEquatorial(galactic, placement.Orientation);
        var (nucleusRa, nucleusDec) = placement.Orientation.ToEquatorial(0.0, 0.0);
        var (poleRa, poleDec) = placement.Orientation.ToEquatorial(0.0, Math.PI / 2.0);
        var nucleus = SampleEquatorial(equatorial, nucleusRa, nucleusDec);
        var pole = SampleEquatorial(equatorial, poleRa, poleDec);

        Assert.True(
            nucleus.Red + nucleus.Green + nucleus.Blue > pole.Red + pole.Green + pole.Blue + 40,
            "the nucleus should be brighter than the galactic pole on the equatorial map");
    }

    private static (byte Red, byte Green, byte Blue) SampleEquatorial(byte[] rgb, double rightAscension, double declination)
    {
        var (u, v) = EquirectangularSampler.EquatorialUv(rightAscension, declination);
        return EquirectangularSampler.Sample(rgb, GalaxySkyView.Width, GalaxySkyView.Height, u, v);
    }
}
