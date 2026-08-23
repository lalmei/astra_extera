using AstraExtera.Galaxy;
using Xunit;

namespace AstraExtera.Tests.Galaxy;

/// <summary>
/// The projection is shared by the SVG on the debug page and the Cairo drawing in the game panel,
/// so a point that lands off-figure is a bug in both at once.
/// </summary>
public sealed class GalaxyFigureGeometryTests
{
    [Theory]
    [InlineData(42)]
    [InlineData(7)]
    [InlineData(1979)]
    [InlineData(20260822)]
    public void The_Observer_Lands_Inside_Both_Figures(int seed)
    {
        var placement = GalaxyGenerator.Generate(seed);

        var face = GalaxyFigureGeometry.FacePoint(
            placement.Location.GalactocentricRadiusKpc,
            placement.Location.AzimuthRad);
        Assert.InRange(face.X, 0.0, GalaxyFigureGeometry.FaceSize);
        Assert.InRange(face.Y, 0.0, GalaxyFigureGeometry.FaceSize);

        var edgeX = GalaxyFigureGeometry.EdgeX(placement.Location.GalactocentricRadiusKpc);
        var edgeY = GalaxyFigureGeometry.EdgeY(placement.Galaxy, placement.Location.HeightPc);
        Assert.InRange(edgeX, GalaxyFigureGeometry.EdgePadX, GalaxyFigureGeometry.EdgePadX + GalaxyFigureGeometry.EdgePlotWidth);
        Assert.InRange(edgeY, GalaxyFigureGeometry.EdgePadY, GalaxyFigureGeometry.EdgePadY + GalaxyFigureGeometry.EdgePlotHeight);
    }

    [Fact]
    public void The_Galactic_Centre_Sits_At_The_Middle_Of_The_Face_On_Figure()
    {
        var centre = GalaxyFigureGeometry.FacePoint(0.0, 1.234);

        Assert.Equal(GalaxyFigureGeometry.FaceCx, centre.X, 9);
        Assert.Equal(GalaxyFigureGeometry.FaceCy, centre.Y, 9);
    }

    [Fact]
    public void The_Midplane_Is_The_Middle_Of_The_Edge_On_Figure()
    {
        var placement = GalaxyGenerator.Generate(42);

        Assert.Equal(GalaxyFigureGeometry.EdgeMidY, GalaxyFigureGeometry.EdgeY(placement.Galaxy, 0.0), 9);
        Assert.True(GalaxyFigureGeometry.EdgeY(placement.Galaxy, 200.0) < GalaxyFigureGeometry.EdgeMidY);
    }

    [Fact]
    public void Spiral_Arms_Are_Traced_Across_The_Whole_Disk_And_Stay_On_The_Figure()
    {
        var placement = GalaxyGenerator.Generate(42);
        Assert.False(placement.Galaxy.IsElliptical);

        for (var arm = 0; arm < placement.Galaxy.SpiralArmCount; arm++)
        {
            var points = GalaxyFigureGeometry.ArmPoints(placement.Galaxy, arm);

            Assert.True(points.Count > 2);
            Assert.All(points, static point =>
            {
                Assert.InRange(point.X, 0.0, GalaxyFigureGeometry.FaceSize);
                Assert.InRange(point.Y, 0.0, GalaxyFigureGeometry.FaceSize);
            });

            var span = Distance(points[0], points[^1]);
            Assert.True(span > 20.0, $"arm {arm} was traced over only {span:0.0} px");
        }
    }

    [Fact]
    public void The_Habitable_Band_Fits_Within_The_Edge_On_Plot()
    {
        for (var seed = 1; seed <= 200; seed++)
        {
            var galaxy = GalaxyGenerator.Generate(seed).Galaxy;
            var band = GalaxyFigureGeometry.EdgeHabitableHeight(galaxy);

            Assert.InRange(band, 0.0, GalaxyFigureGeometry.EdgePlotHeight);
        }
    }

    private static double Distance((double X, double Y) a, (double X, double Y) b)
        => Math.Sqrt(((a.X - b.X) * (a.X - b.X)) + ((a.Y - b.Y) * (a.Y - b.Y)));
}
