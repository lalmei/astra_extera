using AstraExtera.Galaxy;
using Xunit;

namespace AstraExtera.Tests.Galaxy;

public sealed class GalaxySkyTests
{
    [Fact]
    public void Authoring_A_Seed_Samples_The_Catalog_Once()
    {
        var sky = GalaxySky.Author(42);

        Assert.Equal(GalaxyGenerator.Generate(42), sky.Placement);
        Assert.Equal(StarFieldCodec.Quantize(StarFieldSampler.Sample(sky.Placement)).Stars, sky.StarField.Stars);
        Assert.NotEmpty(sky.StarField.Stars);
        Assert.Equal(sky.Placement.System.Companions.Length, sky.LocalSky.Planets.Count);
        Assert.NotEmpty(sky.LocalSky.Comets);
        Assert.NotEmpty(sky.LocalSky.Showers);
    }

    [Fact]
    public void A_Stored_Catalog_Is_The_Sky_Even_When_It_Differs_From_The_Sampler()
    {
        var placement = GalaxyGenerator.Generate(42);
        var stored = new StarField(
            [new VisibleStar(0.1, 0.2, 12.0, 1.0, 2.0, 0.05, 0.65)],
            ExpectedVisibleCount: 1,
            SampledCount: 1,
            LimitingMagnitude: 6.5,
            Truncated: false);

        var resolution = GalaxySkyStore.Resolve(placement, stored, worldSeed: 99);

        Assert.False(resolution.PlacementDirty);
        Assert.False(resolution.StarsDirty);
        Assert.True(resolution.LocalSkyDirty);
        Assert.Equal(placement, resolution.Sky.Placement);
        Assert.Equal(stored.Stars, resolution.Sky.StarField.Stars);
        Assert.NotEqual(StarFieldSampler.Sample(placement).Stars.Count, stored.Stars.Count);
    }

    [Fact]
    public void A_Current_Placement_Without_Stars_Is_A_One_Time_Migration_Sample()
    {
        var placement = GalaxyGenerator.Generate(42);

        var resolution = GalaxySkyStore.Resolve(placement, storedStars: null, worldSeed: 99);

        Assert.False(resolution.PlacementDirty);
        Assert.True(resolution.StarsDirty);
        Assert.True(resolution.LocalSkyDirty);
        Assert.Equal(placement, resolution.Sky.Placement);
        Assert.Equal(StarFieldCodec.Quantize(StarFieldSampler.Sample(placement)).Stars, resolution.Sky.StarField.Stars);
    }

    [Fact]
    public void An_Old_Placement_Schema_Regenerates_The_Galaxy_And_The_Catalog()
    {
        var stale = GalaxyGenerator.Generate(42) with { SchemaVersion = 0 };
        var leftoverStars = new StarField(
            [new VisibleStar(0.1, 0.2, 12.0, 1.0, 2.0, 0.05, 0.65)],
            1,
            1,
            6.5,
            false);

        var resolution = GalaxySkyStore.Resolve(stale, leftoverStars, worldSeed: 7);

        Assert.True(resolution.PlacementDirty);
        Assert.True(resolution.StarsDirty);
        Assert.True(resolution.LocalSkyDirty);
        Assert.Equal(GalaxyPlacement.CurrentSchemaVersion, resolution.Sky.Placement.SchemaVersion);
        Assert.Equal(7, resolution.Sky.Placement.WorldSeed);
        Assert.NotEqual(leftoverStars.Stars, resolution.Sky.StarField.Stars);
    }

    [Fact]
    public void The_Star_Field_Round_Trips_Through_The_Stored_Codec()
    {
        var original = GalaxySky.Author(42).StarField;
        var restored = StarFieldCodec.FromBytes(StarFieldCodec.ToBytes(original));

        Assert.Equal(original.LimitingMagnitude, restored.LimitingMagnitude);
        Assert.Equal(original.ExpectedVisibleCount, restored.ExpectedVisibleCount);
        Assert.Equal(original.SampledCount, restored.SampledCount);
        Assert.Equal(original.Truncated, restored.Truncated);
        Assert.Equal(original.Stars, restored.Stars);
    }

    [Fact]
    public void The_Stored_Catalog_Fits_A_Join_Packet()
    {
        var crowded = GalaxySky.Author(1234);
        var bytes = StarFieldCodec.ToBytes(crowded.StarField);

        Assert.True(crowded.StarField.Stars.Count >= 1000);
        Assert.True(bytes.Length < 250_000, $"stored catalog was {bytes.Length} bytes");
    }
}
