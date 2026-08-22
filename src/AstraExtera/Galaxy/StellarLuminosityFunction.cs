namespace AstraExtera.Galaxy;

/// <summary>
/// Present-day stellar luminosity function for a solar-neighborhood analog: how many stars of a
/// given absolute visual magnitude sit in a cubic parsec. This is what makes the visible star
/// count emerge from the galaxy instead of being chosen -- bright rare stars are visible across
/// kiloparsecs, faint common ones only across a few parsecs.
/// </summary>
public static class StellarLuminosityFunction
{
    public const double BinWidth = 1.0;
    public const double MinAbsoluteMagnitude = -7.0;
    public const double MaxAbsoluteMagnitude = 16.0;

    /// <summary>
    /// Scales the whole function so a solar-neighborhood observer sees an Earth-like naked-eye
    /// count (~9000 stars to magnitude 6.5) once extinction is applied.
    /// </summary>
    public const double Normalization = 5.0;

    /// <summary>Stars per cubic parsec per magnitude at the solar neighborhood, M_V = -7 upward.</summary>
    private static readonly double[] Density =
    [
        2.0e-09, // -7
        8.0e-09, // -6
        3.0e-08, // -5
        1.1e-07, // -4
        4.0e-07, // -3
        1.6e-06, // -2
        6.0e-06, // -1
        2.2e-05, //  0
        6.5e-05, //  1
        1.4e-04, //  2
        2.6e-04, //  3
        4.2e-04, //  4
        4.6e-04, //  5
        4.4e-04, //  6
        5.0e-04, //  7
        6.2e-04, //  8
        8.2e-04, //  9
        1.2e-03, // 10
        1.8e-03, // 11
        2.6e-03, // 12
        3.2e-03, // 13
        3.0e-03, // 14
        2.0e-03, // 15
        1.0e-03  // 16
    ];

    public static int BinCount => Density.Length;

    public static double BinAbsoluteMagnitude(int bin)
        => MinAbsoluteMagnitude + bin * BinWidth;

    /// <summary>Stars per cubic parsec per magnitude at solar density for this bin.</summary>
    public static double BinDensity(int bin)
        => Density[bin] * Normalization;
}
