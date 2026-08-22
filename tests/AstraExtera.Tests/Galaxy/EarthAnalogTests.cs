using AstraExtera.Galaxy;
using Xunit;

namespace AstraExtera.Tests.Galaxy;

public sealed class EarthAnalogTests
{
    [Fact]
    public void Sampled_Worlds_Stay_Near_Earth_Radius_Gravity_Iron_And_Temperature()
    {
        var rng = new SplitMix64(12345);
        for (var i = 0; i < 80; i++)
        {
            var world = EarthAnalog.Sample(ref rng);
            Assert.True(EarthAnalog.IsEarthlike(world));
            Assert.InRange(world.RadiusEarth, 0.90, 1.10);
            Assert.InRange(world.SurfaceGravityG, 0.90, 1.10);
            Assert.InRange(world.BulkIronMassFraction, 0.28, 0.36);
            Assert.InRange(world.SurfaceTemperatureK, 275.0, 300.0);
        }
    }
}
