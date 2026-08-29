namespace AstraExtera.Galaxy;

/// <summary>
/// The projection behind the face-on and edge-on figures.
/// <para>
/// Shared because the same two pictures are drawn twice by different means: as SVG on the static
/// debug page, and with Cairo in the in-game panel. Holding the geometry here is what makes them
/// the same picture rather than two that slowly disagree.
/// </para>
/// <para>
/// Coordinates are the design space of the original SVG viewBoxes, so a caller drawing at another
/// size scales the whole space rather than recomputing positions.
/// </para>
/// </summary>
public static class GalaxyFigureGeometry
{
    public const double DiskRadiusKpc = 16.0;

    public const double FaceSize = 560.0;
    public const double FaceCx = 280.0;
    public const double FaceCy = 280.0;
    public const double FaceScale = 15.5;

    public const double EdgeViewWidth = 400.0;
    public const double EdgeViewHeight = 280.0;
    public const double EdgePlotHeight = 240.0;
    public const double EdgePadX = 16.0;
    public const double EdgePadY = 20.0;
    public const double EdgePlotWidth = EdgeViewWidth - EdgePadX * 2.0;
    public const double EdgeMidY = EdgePadY + EdgePlotHeight / 2.0;

    private const int ArmSamples = 80;
    private const double ArmInnerRadiusKpc = 1.2;

    public static (double X, double Y) FacePoint(double radiusKpc, double azimuthRad)
        => (
            FaceCx + radiusKpc * Math.Cos(azimuthRad) * FaceScale,
            FaceCy - radiusKpc * Math.Sin(azimuthRad) * FaceScale);

    public static double FaceRadius(double radiusKpc) => radiusKpc * FaceScale;

    /// <summary>Half-height of the edge-on plot in parsecs; ellipticals need a taller box.</summary>
    public static double EdgeExtentPc(GalaxyBlueprint galaxy)
    {
        ArgumentNullException.ThrowIfNull(galaxy);
        return galaxy.IsElliptical
            ? Math.Max(1600.0, galaxy.OuterHabitableRadiusKpc * galaxy.AxisRatio * 1000.0 * 1.2)
            : 1200.0;
    }

    public static double EdgeX(double radiusKpc)
        => EdgePadX + radiusKpc / DiskRadiusKpc * EdgePlotWidth;

    public static double EdgeY(GalaxyBlueprint galaxy, double heightPc)
        => EdgeMidY - heightPc / EdgeExtentPc(galaxy) * (EdgePlotHeight / 2.0);

    /// <summary>Drawn height of the habitable band: three thin-disk scale heights, or the spheroid.</summary>
    public static double EdgeHabitableHeight(GalaxyBlueprint galaxy)
    {
        ArgumentNullException.ThrowIfNull(galaxy);
        var heightPc = galaxy.IsElliptical
            ? galaxy.OuterHabitableRadiusKpc * galaxy.AxisRatio * 1000.0
            : 3.0 * galaxy.ThinDiskScaleHeightPc;
        return Math.Min(EdgePlotHeight - 16.0, heightPc / EdgeExtentPc(galaxy) * EdgePlotHeight);
    }

    /// <summary>The traced centreline of one spiral arm, in face-on design coordinates.</summary>
    public static List<(double X, double Y)> ArmPoints(GalaxyBlueprint galaxy, int arm)
    {
        ArgumentNullException.ThrowIfNull(galaxy);
        var points = new List<(double X, double Y)>(ArmSamples + 1);
        for (var i = 0; i <= ArmSamples; i++)
        {
            var radius = ArmInnerRadiusKpc + (DiskRadiusKpc - ArmInnerRadiusKpc) * (i / (double)ArmSamples);
            points.Add(FacePoint(radius, GalaxyGenerator.SpiralArmAngleRad(galaxy, arm, radius)));
        }

        return points;
    }

    /// <summary>Isophote radii for an elliptical, as multiples of the effective radius.</summary>
    public static double[] EllipticalIsophoteFractions => [0.4, 0.7, 1.0, 1.4, 1.9];

    /// <summary>Half-length of a bar, drawn only for barred spirals.</summary>
    public static double FaceBarHalfLength(GalaxyBlueprint galaxy)
    {
        ArgumentNullException.ThrowIfNull(galaxy);
        return FaceRadius(galaxy.InnerHabitableRadiusKpc * 0.55);
    }
}
