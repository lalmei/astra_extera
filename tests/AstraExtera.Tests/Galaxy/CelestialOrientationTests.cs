using AstraExtera.Galaxy;
using Xunit;

namespace AstraExtera.Tests.Galaxy;

public sealed class CelestialOrientationTests
{
    /// <summary>
    /// The sky's orientation is a world property every player must share, so it has to survive the
    /// save and sync round trip rather than being re-rolled per client.
    /// </summary>
    [Fact]
    public void Orientation_Survives_The_Save_Round_Trip()
    {
        var placement = GalaxyGenerator.Generate(42);
        var restored = GalaxyPlacementCodec.FromUtf8(GalaxyPlacementCodec.ToUtf8(placement));

        Assert.Equal(placement.Orientation, restored.Orientation);
    }

    [Fact]
    public void Different_Worlds_Tilt_Their_Poles_Differently()
    {
        var tilts = new List<double>();
        for (long seed = 1; seed <= 40; seed++)
        {
            tilts.Add(GalaxyGenerator.Generate(seed).Orientation.PoleTiltFromGalacticPoleDeg);
        }

        Assert.All(tilts, tilt => Assert.InRange(tilt, 0.0, 90.0));
        Assert.True(tilts.Distinct().Count() > 30);
        Assert.True(tilts.Max() - tilts.Min() > 30.0);
    }

    [Fact]
    public void Equatorial_And_Galactic_Round_Trip()
    {
        var orientation = GalaxyGenerator.Generate(42).Orientation;
        for (var longitude = -Math.PI; longitude < Math.PI; longitude += Math.PI / 5.0)
        {
            for (var latitude = -Math.PI / 3.0; latitude <= Math.PI / 3.0; latitude += Math.PI / 6.0)
            {
                var (rightAscension, declination) = orientation.ToEquatorial(longitude, latitude);
                var (backLongitude, backLatitude) = orientation.ToGalactic(rightAscension, declination);
                var longitudeError = Math.Abs(Math.Atan2(
                    Math.Sin(backLongitude - longitude),
                    Math.Cos(backLongitude - longitude)));
                Assert.True(longitudeError < 1e-9, $"longitude drifted by {longitudeError}");
                Assert.True(Math.Abs(backLatitude - latitude) < 1e-9, $"latitude drifted by {backLatitude - latitude}");
            }
        }
    }
}
