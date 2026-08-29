using AstraExtera.Galaxy;
using Xunit;

namespace AstraExtera.Tests.Galaxy;

public sealed class GalaxyFactsTests
{
    [Fact]
    public void Every_Section_Describes_Something()
    {
        var placement = GalaxyGenerator.Generate(42);
        var sections = GalaxyFacts.Describe(placement, StarFieldSampler.Sample(placement));

        Assert.Equal("AstraExtera galaxy preview - seed 42", GalaxyFacts.Title(placement));
        Assert.Equal("Host galaxy - seed 42", GalaxyFacts.PanelTitle(placement));
        Assert.Equal(["Galaxy", "Observer", "Local system", "Earth analog", "Visible sky"], sections.Select(static s => s.Heading));
        Assert.Equal(
            ["Galaxy", "Observer", "Local system", "Earth analog", "Visible sky", "Wanderers"],
            GalaxyFacts.Describe(GalaxySky.Author(42)).Select(static s => s.Heading));
        Assert.All(sections, static section =>
        {
            Assert.NotEmpty(section.Rows);
            Assert.All(section.Rows, static row =>
            {
                Assert.False(string.IsNullOrWhiteSpace(row.Term));
                Assert.False(string.IsNullOrWhiteSpace(row.Value));
            });
        });
    }

    /// <summary>
    /// The panel draws these strings with the game font, which is not guaranteed to carry the
    /// astronomical symbols. A solar-mass sign that renders as a missing-glyph box in game is worse
    /// than spelling the unit out, so the shared facts stay inside Latin-1.
    /// </summary>
    [Theory]
    [InlineData(42)]
    [InlineData(7)]
    [InlineData(1979)]
    public void Values_Stay_Inside_Latin1_So_The_Game_Font_Can_Draw_Them(int seed)
    {
        var placement = GalaxyGenerator.Generate(seed);
        var sections = GalaxyFacts.Describe(GalaxySky.Author(placement));

        foreach (var row in sections.SelectMany(static section => section.Rows))
        {
            foreach (var character in row.Term + row.Value)
            {
                Assert.True(
                    character <= 0xFF,
                    $"'{row.Term}' carries U+{(int)character:X4} '{character}', outside Latin-1");
            }
        }
    }

    [Fact]
    public void An_Elliptical_Host_Reports_A_Spheroid_Rather_Than_A_Disk()
    {
        var placement = EllipticalPlacement();
        var rows = GalaxyFacts.Describe(placement, StarFieldSampler.Sample(placement))
            .SelectMany(static section => section.Rows)
            .ToDictionary(static row => row.Term, static row => row.Value);

        Assert.True(rows.ContainsKey("Spheroid"));
        Assert.False(rows.ContainsKey("Disk"));
        Assert.Equal("none", rows["Spiral arm"]);
        Assert.Contains("Sersic", rows["Morphology"], StringComparison.Ordinal);
    }

    [Fact]
    public void The_Page_And_The_Panel_Read_From_The_Same_Facts()
    {
        var placement = GalaxyGenerator.Generate(42);
        var starField = StarFieldSampler.Sample(placement);
        var html = GalaxyDebugHtml.Render(placement, starField);

        foreach (var row in GalaxyFacts.Describe(placement, starField).SelectMany(static section => section.Rows))
        {
            Assert.Contains(row.Term, html, StringComparison.Ordinal);
        }
    }

    private static GalaxyPlacement EllipticalPlacement()
    {
        for (var seed = 1; seed < 4000; seed++)
        {
            var placement = GalaxyGenerator.Generate(seed);
            if (placement.Galaxy.IsElliptical)
            {
                return placement;
            }
        }

        throw new InvalidOperationException("no elliptical host galaxy was generated in the searched seed range");
    }
}
