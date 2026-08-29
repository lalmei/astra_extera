using System.IO.Compression;
using System.Text;

namespace AstraExtera.Galaxy;

/// <summary>
/// Compact stored form of a sampled star field. Gzipped binary, with per-star values as single
/// precision, so a 10,000-star catalog stays small enough to live on the save and ride the join
/// packet. The live sky uses this stored list; it does not resample.
/// </summary>
public static class StarFieldCodec
{
    public const int CurrentSchemaVersion = 1;

    private static readonly byte[] Magic = "AESF"u8.ToArray();

    /// <summary>
    /// Snaps per-star values to the precision that is actually stored, so the in-memory catalog and
    /// the save blob are the same sky.
    /// </summary>
    public static StarField Quantize(StarField field)
    {
        ArgumentNullException.ThrowIfNull(field);
        var stars = new VisibleStar[field.Stars.Count];
        for (var i = 0; i < stars.Length; i++)
        {
            var star = field.Stars[i];
            stars[i] = new VisibleStar(
                (float)star.GalacticLongitudeRad,
                (float)star.GalacticLatitudeRad,
                (float)star.DistancePc,
                (float)star.AbsoluteMagnitude,
                (float)star.ApparentMagnitude,
                (float)star.ExtinctionMagnitudes,
                (float)star.ColorIndexBv);
        }

        return new StarField(
            stars,
            field.ExpectedVisibleCount,
            field.SampledCount,
            field.LimitingMagnitude,
            field.Truncated);
    }

    public static byte[] ToBytes(StarField field)
    {
        ArgumentNullException.ThrowIfNull(field);

        using var buffer = new MemoryStream();
        using (var gzip = new GZipStream(buffer, CompressionLevel.Fastest, leaveOpen: true))
        using (var writer = new BinaryWriter(gzip, Encoding.UTF8, leaveOpen: true))
        {
            writer.Write(Magic);
            writer.Write(CurrentSchemaVersion);
            writer.Write(field.LimitingMagnitude);
            writer.Write(field.ExpectedVisibleCount);
            writer.Write(field.SampledCount);
            writer.Write(field.Truncated);
            writer.Write(field.Stars.Count);
            foreach (var star in field.Stars)
            {
                writer.Write((float)star.GalacticLongitudeRad);
                writer.Write((float)star.GalacticLatitudeRad);
                writer.Write((float)star.DistancePc);
                writer.Write((float)star.AbsoluteMagnitude);
                writer.Write((float)star.ApparentMagnitude);
                writer.Write((float)star.ExtinctionMagnitudes);
                writer.Write((float)star.ColorIndexBv);
            }
        }

        return buffer.ToArray();
    }

    public static StarField FromBytes(byte[] bytes)
    {
        ArgumentNullException.ThrowIfNull(bytes);
        if (bytes.Length == 0)
        {
            throw new InvalidOperationException("Star field payload was empty.");
        }

        using var input = new MemoryStream(bytes);
        using var gzip = new GZipStream(input, CompressionMode.Decompress);
        using var reader = new BinaryReader(gzip, Encoding.UTF8, leaveOpen: true);

        var magic = reader.ReadBytes(Magic.Length);
        if (!magic.AsSpan().SequenceEqual(Magic))
        {
            throw new InvalidOperationException("Star field payload was not an AstraExtera catalog.");
        }

        var version = reader.ReadInt32();
        if (version != CurrentSchemaVersion)
        {
            throw new InvalidOperationException($"Star field schema {version} is not supported.");
        }

        var limitingMagnitude = reader.ReadDouble();
        var expectedVisibleCount = reader.ReadDouble();
        var sampledCount = reader.ReadInt32();
        var truncated = reader.ReadBoolean();
        var starCount = reader.ReadInt32();
        if (starCount < 0)
        {
            throw new InvalidOperationException("Star field payload listed a negative star count.");
        }

        var stars = new VisibleStar[starCount];
        for (var i = 0; i < starCount; i++)
        {
            stars[i] = new VisibleStar(
                reader.ReadSingle(),
                reader.ReadSingle(),
                reader.ReadSingle(),
                reader.ReadSingle(),
                reader.ReadSingle(),
                reader.ReadSingle(),
                reader.ReadSingle());
        }

        return new StarField(stars, expectedVisibleCount, sampledCount, limitingMagnitude, truncated);
    }
}
