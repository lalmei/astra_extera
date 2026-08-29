namespace AstraExtera.Galaxy;

/// <summary>
/// Samples an RGB equirectangular panorama. Shared by the equatorial reproject and the cubemap
/// breakup so both read the same pixels the same way.
/// </summary>
public static class EquirectangularSampler
{
    public static (byte Red, byte Green, byte Blue) Sample(
        byte[] rgb,
        int width,
        int height,
        double u,
        double v)
    {
        ArgumentNullException.ThrowIfNull(rgb);
        if (width <= 0 || height <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(width));
        }

        u = u - Math.Floor(u);
        v = Math.Clamp(v, 0.0, 1.0);
        var x = u * width - 0.5;
        var y = v * height - 0.5;
        var x0 = Mod(Math.Floor(x), width);
        var y0 = Math.Clamp((int)Math.Floor(y), 0, height - 1);
        var x1 = (x0 + 1) % width;
        var y1 = Math.Min(y0 + 1, height - 1);
        var fx = x - Math.Floor(x);
        var fy = y - y0;

        var a = Pixel(rgb, width, x0, y0);
        var b = Pixel(rgb, width, x1, y0);
        var c = Pixel(rgb, width, x0, y1);
        var d = Pixel(rgb, width, x1, y1);
        return (
            (byte)Math.Clamp(Lerp(Lerp(a.Red, b.Red, fx), Lerp(c.Red, d.Red, fx), fy), 0, 255),
            (byte)Math.Clamp(Lerp(Lerp(a.Green, b.Green, fx), Lerp(c.Green, d.Green, fx), fy), 0, 255),
            (byte)Math.Clamp(Lerp(Lerp(a.Blue, b.Blue, fx), Lerp(c.Blue, d.Blue, fx), fy), 0, 255));
    }

    public static (double U, double V) GalacticUv(double longitudeRad, double latitudeRad)
        => ((longitudeRad + Math.PI) / (2.0 * Math.PI), (Math.PI / 2.0 - latitudeRad) / Math.PI);

    public static (double U, double V) EquatorialUv(double rightAscensionDeg, double declinationDeg)
        => (rightAscensionDeg / 360.0, (90.0 - declinationDeg) / 180.0);

    private static (byte Red, byte Green, byte Blue) Pixel(byte[] rgb, int width, int x, int y)
    {
        var i = (y * width + x) * 3;
        return (rgb[i], rgb[i + 1], rgb[i + 2]);
    }

    private static double Lerp(double a, double b, double t)
        => a + (b - a) * t;

    private static int Mod(double value, int modulus)
    {
        var n = (int)value % modulus;
        return n < 0 ? n + modulus : n;
    }
}
