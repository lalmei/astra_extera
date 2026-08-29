using AstraExtera.Galaxy;
using Xunit;

namespace AstraExtera.Tests.Galaxy;

public sealed class GalaxyGeneratorTests
{
    [Fact]
    public void The_Same_World_Seed_Authors_The_Same_Galaxy()
    {
        var first = GalaxyGenerator.Generate(90210);
        var second = GalaxyGenerator.Generate(90210);

        Assert.Equal(first, second);
    }

    [Fact]
    public void A_Different_Seed_Authors_A_Different_Location()
    {
        var first = GalaxyGenerator.Generate(1);
        var second = GalaxyGenerator.Generate(2);

        Assert.NotEqual(first.Location.GalactocentricRadiusKpc, second.Location.GalactocentricRadiusKpc);
        Assert.NotEqual(first.Location.AzimuthRad, second.Location.AzimuthRad);
    }

    [Fact]
    public void Every_Authored_World_Can_Host_Iron_And_Ores()
    {
        for (long seed = 1; seed <= 250; seed++)
        {
            var placement = GalaxyGenerator.Generate(seed);

            Assert.True(
                GalaxyGenerator.IsHabitable(placement.Galaxy, placement.Location),
                $"seed {seed} left the galactic habitable zone");
            Assert.True(placement.CanHostIronCore, $"seed {seed} lacked iron");
            Assert.True(placement.CanHostOres, $"seed {seed} lacked ores");
            Assert.InRange(
                GalaxyGenerator.StructuralRadiusKpc(placement.Galaxy, placement.Location),
                placement.Galaxy.InnerHabitableRadiusKpc,
                placement.Galaxy.OuterHabitableRadiusKpc);
            Assert.True(EarthAnalog.IsEarthlike(placement.World), $"seed {seed} was not an Earth analog");
            Assert.True(placement.System.IsHabitable, $"seed {seed} local system failed: {string.Join("; ", placement.System.Checks.Where(static c => !c.Passed).Select(static c => c.Detail))}");
        }
    }

    [Fact]
    public void Json_Round_Trips_The_Placement_The_Server_Will_Store()
    {
        var original = GalaxyGenerator.Generate(42);
        var restored = GalaxyPlacementCodec.FromUtf8(GalaxyPlacementCodec.ToUtf8(original));

        Assert.Equal(original, restored);
    }

    [Fact]
    public void Ellipticals_Are_Rare_Spheroids_Without_Arms()
    {
        GalaxyPlacement? elliptical = null;
        var ellipticals = 0;
        const int samples = 800;
        for (long seed = 1; seed <= samples; seed++)
        {
            var placement = GalaxyGenerator.Generate(seed);
            if (placement.Galaxy.Morphology != GalaxyMorphology.Elliptical)
            {
                continue;
            }

            ellipticals++;
            elliptical ??= placement;
            Assert.Equal(0, placement.Galaxy.SpiralArmCount);
            Assert.False(placement.Location.InSpiralArm);
            Assert.True(placement.Galaxy.SersicIndex >= 3.0);
            Assert.True(GalaxyGenerator.IsHabitable(placement.Galaxy, placement.Location));
            Assert.True(placement.CanHostOres);
        }

        Assert.NotNull(elliptical);
        Assert.InRange(ellipticals, 1, samples / 8);
    }
}

public sealed class MetallicityModelTests
{
    [Fact]
    public void Mean_Iron_Falls_Toward_The_Outer_Disk()
    {
        var galaxy = new GalaxyBlueprint(
            GalaxyMorphology.BarredSpiral,
            StellarMassSolar: 6.0e10,
            DiskScaleLengthKpc: 3.0,
            ThinDiskScaleHeightPc: 300.0,
            BulgeToDiskMass: 0.3,
            SolarAnalogMetallicityFeH: 0.0,
            MetallicityGradientDexPerKpc: -0.06,
            MetallicityScatterDex: 0.1,
            SpiralArmCount: 4,
            SpiralPitchDeg: 12.0,
            InnerHabitableRadiusKpc: 6.0,
            OuterHabitableRadiusKpc: 12.0,
            SersicIndex: 1.0,
            AxisRatio: 0.1,
            MetallicityReferenceRadiusKpc: 8.0);

        Assert.Equal(0.0, MetallicityModel.MeanFeH(galaxy, 8.0), 6);
        Assert.True(MetallicityModel.MeanFeH(galaxy, 12.0) < MetallicityModel.MeanFeH(galaxy, 8.0));
        Assert.True(MetallicityModel.MeanFeH(galaxy, 4.0) > MetallicityModel.MeanFeH(galaxy, 8.0));
    }

    [Theory]
    [InlineData(-0.51, false, false)]
    [InlineData(-0.40, true, false)]
    [InlineData(-0.30, true, true)]
    [InlineData(0.00, true, true)]
    public void Iron_And_Ore_Floors_Match_The_Geological_Gates(double feH, bool iron, bool ores)
    {
        Assert.Equal(iron, MetallicityModel.CanHostIronCore(feH));
        Assert.Equal(ores, MetallicityModel.CanHostOres(feH));
    }
}
