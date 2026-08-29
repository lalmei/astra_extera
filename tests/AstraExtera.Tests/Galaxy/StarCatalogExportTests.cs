using System.Text.Json;
using AstraExtera.Galaxy;
using Xunit;

namespace AstraExtera.Tests.Galaxy;

public sealed class StarCatalogExportTests
{
    [Fact]
    public void Every_Star_Lands_In_A_Valid_Equatorial_Position()
    {
        var placement = GalaxyGenerator.Generate(42);
        var entries = StarCatalogExport.BuildEntries(placement, StarFieldSampler.Sample(placement));

        Assert.NotEmpty(entries);
        Assert.All(entries, entry =>
        {
            Assert.True(entry.RightAscensionDeg >= 0.0 && entry.RightAscensionDeg < 360.0);
            Assert.InRange(entry.DeclinationDeg, -90.0, 90.0);
        });
    }

    /// <summary>
    /// A player's constellation is stored as edges between catalog ids, so regenerating the sky for
    /// the same seed has to hand out the same id to the same star or every saved figure scrambles.
    /// </summary>
    [Fact]
    public void Catalog_Ids_Are_Stable_For_A_Given_Seed()
    {
        var first = Build(42);
        var second = Build(42);

        Assert.Equal(first.Count, second.Count);
        Assert.Equal(first, second);

        static IReadOnlyList<AstraTerraStarEntry> Build(long seed)
        {
            var placement = GalaxyGenerator.Generate(seed);
            return StarCatalogExport.BuildEntries(placement, StarFieldSampler.Sample(placement));
        }
    }

    /// <summary>
    /// Ids are handed out by position in the stored field, so a world reloaded from the save has to
    /// keep the same catalog the server first sampled, not draw a new one.
    /// </summary>
    [Fact]
    public void Ids_Survive_The_Save_Round_Trip()
    {
        var sky = GalaxySky.Author(42);
        var restoredPlacement = GalaxyPlacementCodec.FromUtf8(GalaxyPlacementCodec.ToUtf8(sky.Placement));
        var restoredStars = StarFieldCodec.FromBytes(StarFieldCodec.ToBytes(sky.StarField));

        Assert.Equal(
            StarCatalogExport.BuildEntries(sky.Placement, sky.StarField),
            StarCatalogExport.BuildEntries(restoredPlacement, restoredStars));
    }

    [Fact]
    public void Ids_Are_Dense_And_Ranked_By_Brightness()
    {
        var placement = GalaxyGenerator.Generate(42);
        var entries = StarCatalogExport.BuildEntries(placement, StarFieldSampler.Sample(placement));

        for (var i = 0; i < entries.Count; i++)
        {
            Assert.Equal(i + 1, entries[i].Hip);
            if (i > 0)
            {
                Assert.True(entries[i - 1].VisualMagnitude <= entries[i].VisualMagnitude);
            }
        }
    }

    [Fact]
    public void The_Brightest_Stars_Are_Flagged_As_Guides()
    {
        var placement = GalaxyGenerator.Generate(42);
        var options = new StarCatalogExportOptions { GuideStarCount = 12 };
        var entries = StarCatalogExport.BuildEntries(placement, StarFieldSampler.Sample(placement), options);

        Assert.Equal(12, entries.Count(entry => entry.IsGuideStar));
        Assert.All(entries.Take(12), entry => Assert.True(entry.IsGuideStar));
    }

    /// <summary>
    /// AstraTerra reads the catalog with web-style camelCase, so the field names are a contract.
    /// </summary>
    [Fact]
    public void The_Json_Matches_AstraTerras_Star_Catalog_Field_Names()
    {
        var placement = GalaxyGenerator.Generate(42);
        var json = StarCatalogExport.ToJson(placement, StarFieldSampler.Sample(placement));

        using var document = JsonDocument.Parse(json);
        Assert.Equal(JsonValueKind.Array, document.RootElement.ValueKind);
        var star = document.RootElement[0];
        Assert.Equal(1, star.GetProperty("hip").GetInt32());
        Assert.True(star.GetProperty("rightAscensionDeg").GetDouble() >= 0.0);
        Assert.True(star.GetProperty("declinationDeg").GetDouble() >= -90.0);
        Assert.True(star.GetProperty("visualMagnitude").GetDouble() < 7.0);
        Assert.True(star.GetProperty("bvColorIndex").GetDouble() > -1.0);
        Assert.True(star.GetProperty("isGuideStar").GetBoolean());
    }

    [Fact]
    public void The_Catalog_Fits_The_Renderer_Budget()
    {
        var placement = GalaxyGenerator.Generate(1234);
        var field = StarFieldSampler.Sample(placement);

        Assert.True(field.Stars.Count <= new StarFieldOptions().ResolvedStarBudget);
        Assert.Equal(field.Stars.Count, StarCatalogExport.BuildEntries(placement, field).Count);
    }
}
