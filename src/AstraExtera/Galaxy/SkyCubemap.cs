namespace AstraExtera.Galaxy;

public enum SkyCubeFace
{
    PositiveX = 0,
    NegativeX = 1,
    PositiveY = 2,
    NegativeY = 3,
    PositiveZ = 4,
    NegativeZ = 5
}

/// <summary>One vertex of a tessellated cube face, still in equatorial coordinates.</summary>
public readonly record struct SkyCubemapVertex(
    double RightAscensionDeg,
    double DeclinationDeg,
    float U,
    float V);

/// <summary>
/// Breaks an equatorial equirectangular glow map into six cube faces so it can wrap a skybox
/// without pinching at the poles.
/// <para>
/// Faces are in equatorial Cartesian: +Z is the north celestial pole, +X is right ascension 0
/// on the equator. Each face is a square RGB image of <see cref="FaceSize"/> pixels.
/// </para>
/// </summary>
public static class SkyCubemap
{
    public const int FaceSize = 256;
    public const int DefaultSubdivisions = 12;

    public static IReadOnlyList<byte[]> FromEquirectangular(byte[] rgb, int width, int height, int faceSize = FaceSize)
    {
        ArgumentNullException.ThrowIfNull(rgb);
        if (faceSize < 2)
        {
            throw new ArgumentOutOfRangeException(nameof(faceSize));
        }

        var faces = new byte[6][];
        for (var face = 0; face < 6; face++)
        {
            faces[face] = RenderFace(rgb, width, height, (SkyCubeFace)face, faceSize);
        }

        return faces;
    }

    public static byte[] RenderFace(byte[] rgb, int width, int height, SkyCubeFace face, int faceSize = FaceSize)
    {
        ArgumentNullException.ThrowIfNull(rgb);
        var pixels = new byte[faceSize * faceSize * 3];
        for (var y = 0; y < faceSize; y++)
        {
            var v = (y + 0.5) / faceSize;
            for (var x = 0; x < faceSize; x++)
            {
                var u = (x + 0.5) / faceSize;
                var (dx, dy, dz) = FaceDirection(face, u, v);
                var rightAscension = Math.Atan2(dy, dx) * 180.0 / Math.PI;
                if (rightAscension < 0.0)
                {
                    rightAscension += 360.0;
                }

                var declination = Math.Asin(Math.Clamp(dz, -1.0, 1.0)) * 180.0 / Math.PI;
                var (s, t) = EquirectangularSampler.EquatorialUv(rightAscension, declination);
                var sample = EquirectangularSampler.Sample(rgb, width, height, s, t);
                var i = (y * faceSize + x) * 3;
                pixels[i] = sample.Red;
                pixels[i + 1] = sample.Green;
                pixels[i + 2] = sample.Blue;
            }
        }

        return pixels;
    }

    public static string FaceLabel(SkyCubeFace face)
        => face switch
        {
            SkyCubeFace.PositiveX => "+X  RA 0h",
            SkyCubeFace.NegativeX => "-X  RA 12h",
            SkyCubeFace.PositiveY => "+Y  RA 6h",
            SkyCubeFace.NegativeY => "-Y  RA 18h",
            SkyCubeFace.PositiveZ => "+Z  NCP",
            SkyCubeFace.NegativeZ => "-Z  SCP",
            _ => face.ToString()
        };

    public static string FaceHtmlId(SkyCubeFace face)
        => face switch
        {
            SkyCubeFace.PositiveX => "sky-cube-px",
            SkyCubeFace.NegativeX => "sky-cube-nx",
            SkyCubeFace.PositiveY => "sky-cube-py",
            SkyCubeFace.NegativeY => "sky-cube-ny",
            SkyCubeFace.PositiveZ => "sky-cube-pz",
            SkyCubeFace.NegativeZ => "sky-cube-nz",
            _ => "sky-cube"
        };

    /// <summary>
    /// Tessellated grid for one face. U and V are catalog coordinates: v = 0 is the top of the face
    /// image. The in-game mesh inverts V to match Vintage Story's texture upload.
    /// </summary>
    public static SkyCubemapVertex[] FaceGrid(SkyCubeFace face, int subdivisions = DefaultSubdivisions)
    {
        if (subdivisions < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(subdivisions));
        }

        var rowSize = subdivisions + 1;
        var vertices = new SkyCubemapVertex[rowSize * rowSize];
        var index = 0;
        for (var y = 0; y <= subdivisions; y++)
        {
            var v = y / (double)subdivisions;
            for (var x = 0; x <= subdivisions; x++)
            {
                var u = x / (double)subdivisions;
                var (dx, dy, dz) = FaceDirection(face, u, v);
                var rightAscension = Math.Atan2(dy, dx) * 180.0 / Math.PI;
                if (rightAscension < 0.0)
                {
                    rightAscension += 360.0;
                }

                var declination = Math.Asin(Math.Clamp(dz, -1.0, 1.0)) * 180.0 / Math.PI;
                vertices[index++] = new SkyCubemapVertex(
                    rightAscension,
                    declination,
                    (float)u,
                    (float)v);
            }
        }

        return vertices;
    }

    public static int[] FaceIndices(int subdivisions = DefaultSubdivisions)
    {
        if (subdivisions < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(subdivisions));
        }

        var rowSize = subdivisions + 1;
        var indices = new int[subdivisions * subdivisions * 6];
        var index = 0;
        for (var row = 0; row < subdivisions; row++)
        {
            for (var column = 0; column < subdivisions; column++)
            {
                var topLeft = (row * rowSize) + column;
                var topRight = topLeft + 1;
                var bottomLeft = topLeft + rowSize;
                var bottomRight = bottomLeft + 1;
                indices[index++] = topLeft;
                indices[index++] = topRight;
                indices[index++] = bottomRight;
                indices[index++] = topLeft;
                indices[index++] = bottomRight;
                indices[index++] = bottomLeft;
            }
        }

        return indices;
    }

    /// <summary>
    /// Direction through a cube-face pixel, u and v in [0, 1], origin at the top-left of the face.
    /// </summary>
    public static (double X, double Y, double Z) FaceDirection(SkyCubeFace face, double u, double v)
    {
        var sc = 2.0 * u - 1.0;
        var tc = 2.0 * v - 1.0;
        var (x, y, z) = face switch
        {
            SkyCubeFace.PositiveX => (1.0, -tc, -sc),
            SkyCubeFace.NegativeX => (-1.0, -tc, sc),
            SkyCubeFace.PositiveY => (sc, 1.0, tc),
            SkyCubeFace.NegativeY => (sc, -1.0, -tc),
            SkyCubeFace.PositiveZ => (sc, -tc, 1.0),
            SkyCubeFace.NegativeZ => (-sc, -tc, -1.0),
            _ => (1.0, 0.0, 0.0)
        };

        var length = Math.Sqrt(x * x + y * y + z * z);
        return (x / length, y / length, z / length);
    }
}
