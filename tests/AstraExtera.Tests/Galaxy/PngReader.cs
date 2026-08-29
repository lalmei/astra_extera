using System.Buffers.Binary;
using System.IO.Compression;

namespace AstraExtera.Tests.Galaxy;

/// <summary>
/// A minimal PNG reader, for tests that want to run the real shipped artwork through the real
/// compositor. The mod itself never needs one -- the game decodes its own assets -- so this lives
/// with the tests rather than in the mod.
/// </summary>
/// <remarks>
/// Handles what the preparation step writes and nothing else: 8-bit RGBA, non-interlaced.
/// </remarks>
public static class PngReader
{
    public static (int Size, int[] Rgba) ReadSquare(string path)
    {
        var bytes = File.ReadAllBytes(path);
        var width = BinaryPrimitives.ReadInt32BigEndian(bytes.AsSpan(16, 4));
        var height = BinaryPrimitives.ReadInt32BigEndian(bytes.AsSpan(20, 4));
        var bitDepth = bytes[24];
        var colourType = bytes[25];
        var interlace = bytes[28];
        if (bitDepth != 8 || colourType != 6 || interlace != 0 || width != height)
        {
            throw new NotSupportedException(
                $"{path} is {width}x{height} depth {bitDepth} type {colourType} interlace {interlace}; " +
                "the reader handles square 8-bit RGBA only.");
        }

        var compressed = new MemoryStream();
        var offset = 8;
        while (offset + 8 <= bytes.Length)
        {
            var length = BinaryPrimitives.ReadInt32BigEndian(bytes.AsSpan(offset, 4));
            var type = System.Text.Encoding.ASCII.GetString(bytes, offset + 4, 4);
            if (type == "IDAT")
            {
                compressed.Write(bytes, offset + 8, length);
            }

            offset += length + 12;
            if (type == "IEND")
            {
                break;
            }
        }

        compressed.Position = 0;
        using var inflate = new ZLibStream(compressed, CompressionMode.Decompress);
        using var raw = new MemoryStream();
        inflate.CopyTo(raw);
        var data = raw.ToArray();

        const int bytesPerPixel = 4;
        var stride = width * bytesPerPixel;
        var pixels = new int[width * height];
        var previous = new byte[stride];
        var current = new byte[stride];

        for (var row = 0; row < height; row++)
        {
            var start = row * (stride + 1);
            var filter = data[start];
            Array.Copy(data, start + 1, current, 0, stride);

            for (var i = 0; i < stride; i++)
            {
                var left = i >= bytesPerPixel ? current[i - bytesPerPixel] : 0;
                var up = previous[i];
                var upLeft = i >= bytesPerPixel ? previous[i - bytesPerPixel] : 0;
                current[i] = filter switch
                {
                    0 => current[i],
                    1 => (byte)(current[i] + left),
                    2 => (byte)(current[i] + up),
                    3 => (byte)(current[i] + ((left + up) / 2)),
                    4 => (byte)(current[i] + Paeth(left, up, upLeft)),
                    _ => throw new NotSupportedException($"Unknown PNG filter {filter}.")
                };
            }

            for (var column = 0; column < width; column++)
            {
                var i = column * bytesPerPixel;
                pixels[(row * width) + column] = AstraExtera.Galaxy.CelestialFaceComposer.Pack(
                    current[i],
                    current[i + 1],
                    current[i + 2],
                    current[i + 3]);
            }

            (previous, current) = (current, previous);
        }

        return (width, pixels);
    }

    private static byte Paeth(int left, int up, int upLeft)
    {
        var estimate = left + up - upLeft;
        var distanceLeft = Math.Abs(estimate - left);
        var distanceUp = Math.Abs(estimate - up);
        var distanceUpLeft = Math.Abs(estimate - upLeft);
        if (distanceLeft <= distanceUp && distanceLeft <= distanceUpLeft)
        {
            return (byte)left;
        }

        return (byte)(distanceUp <= distanceUpLeft ? up : upLeft);
    }
}
