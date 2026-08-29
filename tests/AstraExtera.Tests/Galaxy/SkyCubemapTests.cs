using AstraExtera.Galaxy;
using Xunit;

namespace AstraExtera.Tests.Galaxy;

public sealed class SkyCubemapTests
{
    [Theory]
    [InlineData(SkyCubeFace.PositiveX, 0.0, 0.0)]
    [InlineData(SkyCubeFace.PositiveY, 90.0, 0.0)]
    [InlineData(SkyCubeFace.NegativeX, 180.0, 0.0)]
    [InlineData(SkyCubeFace.NegativeY, 270.0, 0.0)]
    public void Equatorial_Face_Centers_Sit_On_The_Equator(SkyCubeFace face, double rightAscensionDeg, double declinationDeg)
    {
        var (x, y, z) = SkyCubemap.FaceDirection(face, 0.5, 0.5);
        var rightAscension = Math.Atan2(y, x) * 180.0 / Math.PI;
        if (rightAscension < 0.0)
        {
            rightAscension += 360.0;
        }

        var declination = Math.Asin(Math.Clamp(z, -1.0, 1.0)) * 180.0 / Math.PI;
        Assert.Equal(rightAscensionDeg, rightAscension, 6);
        Assert.Equal(declinationDeg, declination, 6);
    }

    [Fact]
    public void The_Positive_Z_Face_Is_The_North_Celestial_Pole()
    {
        var (_, _, z) = SkyCubemap.FaceDirection(SkyCubeFace.PositiveZ, 0.5, 0.5);
        Assert.Equal(1.0, z, 6);
    }

    [Fact]
    public void The_Negative_Z_Face_Is_The_South_Celestial_Pole()
    {
        var (_, _, z) = SkyCubemap.FaceDirection(SkyCubeFace.NegativeZ, 0.5, 0.5);
        Assert.Equal(-1.0, z, 6);
    }

    [Fact]
    public void Cubemap_Faces_Cover_The_Same_Glow_As_The_Equatorial_Map()
    {
        var placement = GalaxyGenerator.Generate(42);
        var equatorial = GalaxySkyView.RenderEquatorialGlowRgb(placement);
        var faces = SkyCubemap.FromEquirectangular(equatorial, GalaxySkyView.Width, GalaxySkyView.Height, faceSize: 32);

        Assert.Equal(6, faces.Count);
        Assert.All(faces, face => Assert.Equal(32 * 32 * 3, face.Length));

        var equatorialMean = Mean(equatorial);
        var cubemapMean = faces.Average(Mean);
        Assert.InRange(cubemapMean, equatorialMean * 0.6, equatorialMean * 1.4);
    }

    [Fact]
    public void Face_Grid_Centers_Match_Face_Directions()
    {
        var grid = SkyCubemap.FaceGrid(SkyCubeFace.PositiveX, subdivisions: 2);
        var center = grid[4];
        Assert.Equal(0.0, center.RightAscensionDeg, 6);
        Assert.Equal(0.0, center.DeclinationDeg, 6);
        Assert.Equal(0.5, center.U, 5);
        Assert.Equal(0.5, center.V, 5);
    }

    private static double Mean(byte[] rgb)
    {
        var sum = 0.0;
        for (var i = 0; i < rgb.Length; i++)
        {
            sum += rgb[i];
        }

        return sum / rgb.Length;
    }
}
