using System.Buffers.Binary;
using System.IO.Compression;
using System.Text;

namespace AstraExtera.Galaxy;

/// <summary>
/// Quick all-sky rendering of the host galaxy as it would appear from the authored world.
/// Galactic longitude 0 is the nucleus; the midplane is the equator of the map.
/// </summary>
public static class GalaxySkyView
{
    public const int Width = 720;
    public const int Height = 360;
    private const int Steps = 96;
    private const double MaxDistanceKpc = 28.0;

    /// <summary>Dark navy the panorama sits on, so an empty sky reads as night rather than as a hole.</summary>
    public const int BackgroundRed = 18;
    public const int BackgroundGreen = 16;
    public const int BackgroundBlue = 28;
    public const int BackgroundSum = BackgroundRed + BackgroundGreen + BackgroundBlue;

    public static string RenderPngDataUri(GalaxyPlacement placement, StarField? starField = null)
        => "data:image/png;base64," + Convert.ToBase64String(RenderPng(placement, starField));

    public static string RenderGlowPngDataUri(GalaxyPlacement placement)
        => "data:image/png;base64," + Convert.ToBase64String(RgbPng.Encode(Width, Height, RenderGlowRgb(placement)));

    public static string RenderStarOverlayPngDataUri(StarField starField)
        => "data:image/png;base64," + Convert.ToBase64String(RgbPng.Encode(Width, Height, RenderStarOverlayRgb(starField)));

    public static string RenderCubemapFacePngDataUri(byte[] faceRgb, int faceSize)
        => "data:image/png;base64," + Convert.ToBase64String(RgbPng.Encode(faceSize, faceSize, faceRgb));

    public static byte[] RenderPng(GalaxyPlacement placement, StarField? starField = null)
    {
        var rgb = RenderRgb(placement, starField);
        return RgbPng.Encode(Width, Height, rgb);
    }

    public static byte[] RenderRgb(GalaxyPlacement placement, StarField? starField = null)
    {
        var rgb = RenderGlowRgb(placement);
        if (starField is not null)
        {
            SplatStars(rgb, starField);
        }

        return rgb;
    }

    /// <summary>
    /// Unresolved integrated light only: the band the sampler left behind. Stars are a separate
    /// overlay so this map can sit behind them as a sky background.
    /// </summary>
    public static byte[] RenderGlowRgb(GalaxyPlacement placement)
    {
        var galaxy = placement.Galaxy;
        var frame = new ObserverFrame(placement.Location);
        var ds = MaxDistanceKpc / Steps;
        var intensities = new double[Width * Height];
        var maxI = 1e-6;

        for (var y = 0; y < Height; y++)
        {
            var b = Math.PI / 2.0 - (y + 0.5) / Height * Math.PI;
            for (var x = 0; x < Width; x++)
            {
                var l = (x + 0.5) / Width * 2.0 * Math.PI - Math.PI;
                var direction = frame.Direction(l, b);

                var intensity = 0.0;
                var transmittance = 1.0;
                for (var step = 1; step <= Steps; step++)
                {
                    var point = frame.PointAt(direction, step * ds);
                    var stars = GalaxyGenerator.StellarDensityAt(galaxy, point.X, point.Y, point.Z);
                    var dust = GalaxyGenerator.DustDensityAt(galaxy, point.X, point.Y, point.Z);
                    intensity += stars * transmittance * ds;
                    transmittance *= Math.Exp(-dust * ds * 0.22);
                    if (transmittance < 0.01)
                    {
                        break;
                    }
                }

                intensities[y * Width + x] = intensity;
                if (intensity > maxI)
                {
                    maxI = intensity;
                }
            }
        }

        var rgb = new byte[Width * Height * 3];
        var logMax = Math.Log(1.0 + maxI);
        for (var i = 0; i < intensities.Length; i++)
        {
            var t = Math.Log(1.0 + intensities[i]) / logMax;
            t = Math.Clamp(t, 0.0, 1.0);
            var glow = t * t;
            rgb[i * 3] = (byte)Math.Clamp(BackgroundRed + glow * 255.0 * 1.05, 0, 255);
            rgb[i * 3 + 1] = (byte)Math.Clamp(BackgroundGreen + glow * 220.0, 0, 255);
            rgb[i * 3 + 2] = (byte)Math.Clamp(BackgroundBlue + glow * 170.0, 0, 255);
        }

        return rgb;
    }

    /// <summary>
    /// The same unresolved glow, rewritten in equatorial coordinates so it shares the star
    /// catalog's frame and can wrap the sky behind AstraTerra's billboards.
    /// </summary>
    public static byte[] RenderEquatorialGlowRgb(GalaxyPlacement placement)
    {
        ArgumentNullException.ThrowIfNull(placement);
        return ReprojectToEquatorial(RenderGlowRgb(placement), placement.Orientation);
    }

    public static byte[] ReprojectToEquatorial(byte[] galacticRgb, CelestialOrientation orientation)
    {
        ArgumentNullException.ThrowIfNull(galacticRgb);
        ArgumentNullException.ThrowIfNull(orientation);
        var equatorial = new byte[Width * Height * 3];
        for (var y = 0; y < Height; y++)
        {
            var declination = 90.0 - (y + 0.5) / Height * 180.0;
            for (var x = 0; x < Width; x++)
            {
                var rightAscension = (x + 0.5) / Width * 360.0;
                var (longitude, latitude) = orientation.ToGalactic(rightAscension, declination);
                var (u, v) = EquirectangularSampler.GalacticUv(longitude, latitude);
                var sample = EquirectangularSampler.Sample(galacticRgb, Width, Height, u, v);
                var i = (y * Width + x) * 3;
                equatorial[i] = sample.Red;
                equatorial[i + 1] = sample.Green;
                equatorial[i + 2] = sample.Blue;
            }
        }

        return equatorial;
    }

    /// <summary>Resolved stars on black, for compositing over the glow with screen blending.</summary>
    public static byte[] RenderStarOverlayRgb(StarField starField)
    {
        ArgumentNullException.ThrowIfNull(starField);
        var rgb = new byte[Width * Height * 3];
        SplatStars(rgb, starField);
        return rgb;
    }

    /// <summary>
    /// Draws resolved stars over the integrated glow, brightest first, so the map shows both what
    /// the sampler pulled out and the unresolved light it left behind.
    /// </summary>
    private static void SplatStars(byte[] rgb, StarField starField)
    {
        foreach (var star in starField.Stars)
        {
            var x = (int)((star.GalacticLongitudeRad + Math.PI) / (2.0 * Math.PI) * Width);
            var y = (int)((Math.PI / 2.0 - star.GalacticLatitudeRad) / Math.PI * Height);
            if (x < 0 || x >= Width || y < 0 || y >= Height)
            {
                continue;
            }

            var brightness = Math.Clamp(
                (starField.LimitingMagnitude - star.ApparentMagnitude) / 8.0,
                0.05,
                1.0);
            var warmth = Math.Clamp(star.ColorIndexBv, -0.3, 1.6);
            var red = 200.0 + 55.0 * warmth;
            var green = 215.0 - 40.0 * warmth;
            var blue = 245.0 - 120.0 * warmth;
            AddPixel(rgb, x, y, red * brightness, green * brightness, blue * brightness);

            if (star.ApparentMagnitude < 2.0)
            {
                var bleed = 0.35 * brightness;
                AddPixel(rgb, x - 1, y, red * bleed, green * bleed, blue * bleed);
                AddPixel(rgb, x + 1, y, red * bleed, green * bleed, blue * bleed);
                AddPixel(rgb, x, y - 1, red * bleed, green * bleed, blue * bleed);
                AddPixel(rgb, x, y + 1, red * bleed, green * bleed, blue * bleed);
            }
        }
    }

    private static void AddPixel(byte[] rgb, int x, int y, double red, double green, double blue)
    {
        if (x < 0 || x >= Width || y < 0 || y >= Height)
        {
            return;
        }

        var i = (y * Width + x) * 3;
        rgb[i] = (byte)Math.Clamp(rgb[i] + red, 0, 255);
        rgb[i + 1] = (byte)Math.Clamp(rgb[i + 1] + green, 0, 255);
        rgb[i + 2] = (byte)Math.Clamp(rgb[i + 2] + blue, 0, 255);
    }

    public static string Caption(GalaxyPlacement placement)
        => placement.Galaxy.IsElliptical
            ? "All-sky view from this world (galactic coordinates). Unresolved glow is the background; resolved stars sit on top. No thin disk: the old spheroid brightens toward the nucleus at longitude 0deg."
            : "All-sky view from this world (galactic coordinates). Unresolved glow is the background; resolved stars sit on top. The bright band is this galaxy's disk; longitude 0deg is the nucleus. Dust lanes show up if you sit near the midplane.";
}

internal static class RgbPng
{
    public static byte[] Encode(int width, int height, byte[] rgb)
    {
        var stride = width * 3 + 1;
        var raw = new byte[stride * height];
        for (var y = 0; y < height; y++)
        {
            var row = y * stride;
            raw[row] = 0;
            Buffer.BlockCopy(rgb, y * width * 3, raw, row + 1, width * 3);
        }

        using var compressed = new MemoryStream();
        using (var zlib = new ZLibStream(compressed, CompressionLevel.Fastest, leaveOpen: true))
        {
            zlib.Write(raw);
        }

        using var png = new MemoryStream();
        png.Write([137, 80, 78, 71, 13, 10, 26, 10]);
        WriteChunk(png, "IHDR", Ihdr(width, height));
        WriteChunk(png, "IDAT", compressed.ToArray());
        WriteChunk(png, "IEND", []);
        return png.ToArray();
    }

    private static byte[] Ihdr(int width, int height)
    {
        var data = new byte[13];
        BinaryPrimitives.WriteInt32BigEndian(data.AsSpan(0, 4), width);
        BinaryPrimitives.WriteInt32BigEndian(data.AsSpan(4, 4), height);
        data[8] = 8;
        data[9] = 2;
        return data;
    }

    private static void WriteChunk(Stream stream, string type, byte[] data)
    {
        Span<byte> length = stackalloc byte[4];
        BinaryPrimitives.WriteInt32BigEndian(length, data.Length);
        stream.Write(length);
        var typeBytes = Encoding.ASCII.GetBytes(type);
        stream.Write(typeBytes);
        stream.Write(data);
        var crc = Crc32(typeBytes, data);
        Span<byte> crcBytes = stackalloc byte[4];
        BinaryPrimitives.WriteUInt32BigEndian(crcBytes, crc);
        stream.Write(crcBytes);
    }

    private static uint Crc32(byte[] type, byte[] data)
    {
        var crc = 0xFFFFFFFF;
        foreach (var b in type)
        {
            crc = CrcTable[(crc ^ b) & 0xFF] ^ (crc >> 8);
        }

        foreach (var b in data)
        {
            crc = CrcTable[(crc ^ b) & 0xFF] ^ (crc >> 8);
        }

        return crc ^ 0xFFFFFFFF;
    }

    private static readonly uint[] CrcTable = CreateCrcTable();

    private static uint[] CreateCrcTable()
    {
        var table = new uint[256];
        for (uint n = 0; n < 256; n++)
        {
            var c = n;
            for (var k = 0; k < 8; k++)
            {
                c = (c & 1) != 0 ? 0xEDB88320 ^ (c >> 1) : c >> 1;
            }

            table[n] = c;
        }

        return table;
    }
}
