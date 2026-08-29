namespace AstraExtera.Galaxy;

/// <summary>A square RGBA picture: <c>Size * Size</c> pixels, packed the way the game wants them.</summary>
/// <param name="DiscFraction">The body's own globe as a fraction of the picture's half-width.</param>
public sealed record CelestialFace(int Size, int[] Pixels, double DiscFraction);

/// <summary>
/// One source picture and how much of it the body fills, as handed in by whoever loaded it.
/// </summary>
public sealed record CelestialSource(int Size, int[] Pixels, double DiscFraction);

/// <summary>
/// Builds the picture a near body is drawn with: the globe, and the ring system around it, scaled
/// and tilted to the ring the generator actually authored.
/// </summary>
/// <remarks>
/// <para>
/// The shipped ring art is drawn at one fixed tilt, and the giants it has to serve are tipped
/// anywhere from upright to on their side. So the ring is resampled rather than pasted: squashed
/// from the tilt it was drawn at to the tilt this giant has, rolled to the heading its node runs
/// along, and scaled so its outer edge lands where the authored ring ends.
/// </para>
/// <para>
/// The far half of the ring goes down before the globe and the near half after it, which is the one
/// cue that says the planet sits inside its rings rather than behind a halo.
/// </para>
/// <para>
/// Nothing here is lit. The face is assembled flat and fully lit, because the renderer shades it per
/// vertex from wherever the sun is; a second set of shadows baked in here would fight that one.
/// </para>
/// </remarks>
public static class CelestialFaceComposer
{
    /// <summary>Rings closed tighter than this would sample a line of pixels into the whole face.</summary>
    public const double MinOpenness = 0.02;

    /// <summary>How much of the face's half-width the outermost feature reaches.</summary>
    public const double FaceMargin = 0.98;

    public static CelestialFace Compose(
        CelestialSource globe,
        CelestialSource? ring,
        CelestialTexture? ringRecord,
        PlanetRing? authoredRing,
        double openness,
        double rollRadians,
        int size)
    {
        ArgumentNullException.ThrowIfNull(globe);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(size);

        var pixels = new int[size * size];
        var half = size / 2.0;
        var faceRadius = half * FaceMargin;

        var ringOuter = authoredRing?.OuterRadiusPlanetRadii ?? 1.0;
        var drawsRing = ring is not null && ringRecord is not null && authoredRing is not null && ringOuter > 1.0;
        var globeRadius = drawsRing ? faceRadius / ringOuter : faceRadius;
        var discFraction = globeRadius / half;

        if (drawsRing)
        {
            DrawRing(pixels, size, ring!, ringRecord!, faceRadius, openness, rollRadians, front: false);
        }

        DrawGlobe(pixels, size, globe, globeRadius);

        if (drawsRing)
        {
            DrawRing(pixels, size, ring!, ringRecord!, faceRadius, openness, rollRadians, front: true);
        }

        return new CelestialFace(size, pixels, discFraction);
    }

    /// <summary>A face for a body with no artwork: a flat disc in the colour it was authored.</summary>
    public static CelestialFace Flat(int size, float red, float green, float blue)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(size);
        var pixels = new int[size * size];
        var half = size / 2.0;
        var radius = half * FaceMargin;
        var colour = Pack(
            (int)Math.Clamp(red * 255f, 0f, 255f),
            (int)Math.Clamp(green * 255f, 0f, 255f),
            (int)Math.Clamp(blue * 255f, 0f, 255f),
            255);

        for (var y = 0; y < size; y++)
        {
            for (var x = 0; x < size; x++)
            {
                var dx = x + 0.5 - half;
                var dy = y + 0.5 - half;
                if ((dx * dx) + (dy * dy) <= radius * radius)
                {
                    pixels[(y * size) + x] = colour;
                }
            }
        }

        return new CelestialFace(size, pixels, FaceMargin);
    }

    private static void DrawGlobe(int[] pixels, int size, CelestialSource globe, double globeRadius)
    {
        var half = size / 2.0;
        var sourceHalf = globe.Size / 2.0;
        var scale = sourceHalf * Math.Max(0.05, globe.DiscFraction) / Math.Max(1e-6, globeRadius);
        var top = (int)Math.Floor(half - globeRadius);
        var bottom = (int)Math.Ceiling(half + globeRadius);

        for (var y = Math.Max(0, top); y < Math.Min(size, bottom); y++)
        {
            for (var x = Math.Max(0, top); x < Math.Min(size, bottom); x++)
            {
                var dx = x + 0.5 - half;
                var dy = y + 0.5 - half;
                var sample = Sample(globe, sourceHalf + (dx * scale), sourceHalf + (dy * scale));
                pixels[(y * size) + x] = Over(sample, pixels[(y * size) + x]);
            }
        }
    }

    private static void DrawRing(
        int[] pixels,
        int size,
        CelestialSource ring,
        CelestialTexture record,
        double ringOuterPx,
        double openness,
        double rollRadians,
        bool front)
    {
        var half = size / 2.0;
        var sourceHalf = ring.Size / 2.0;
        var sourceOuter = Math.Max(1e-6, record.OuterRadiusFraction * ring.Size);
        var squash = Math.Max(record.BakedOpenness, 1e-4) / Math.Max(openness, MinOpenness);
        var scale = sourceOuter / Math.Max(1e-6, ringOuterPx);
        var cos = Math.Cos(-rollRadians);
        var sin = Math.Sin(-rollRadians);

        for (var y = 0; y < size; y++)
        {
            for (var x = 0; x < size; x++)
            {
                var dx = x + 0.5 - half;
                var dy = y + 0.5 - half;

                // Into the ring's own frame: undo the roll, then undo the squash the tilt applies.
                var u = (dx * cos) - (dy * sin);
                var v = (dx * sin) + (dy * cos);
                if (front != v > 0.0)
                {
                    continue;
                }

                var sample = Sample(ring, sourceHalf + (u * scale), sourceHalf + (v * squash * scale));
                if (sample != 0)
                {
                    pixels[(y * size) + x] = Over(sample, pixels[(y * size) + x]);
                }
            }
        }
    }

    /// <summary>Bilinear sample, transparent outside the picture.</summary>
    private static int Sample(CelestialSource source, double x, double y)
    {
        if (x < 0 || y < 0 || x > source.Size - 1 || y > source.Size - 1)
        {
            return 0;
        }

        var x0 = (int)Math.Floor(x);
        var y0 = (int)Math.Floor(y);
        var x1 = Math.Min(x0 + 1, source.Size - 1);
        var y1 = Math.Min(y0 + 1, source.Size - 1);
        var fx = x - x0;
        var fy = y - y0;

        var topLeft = source.Pixels[(y0 * source.Size) + x0];
        var topRight = source.Pixels[(y0 * source.Size) + x1];
        var bottomLeft = source.Pixels[(y1 * source.Size) + x0];
        var bottomRight = source.Pixels[(y1 * source.Size) + x1];

        var red = Blend(Red(topLeft), Red(topRight), Red(bottomLeft), Red(bottomRight), fx, fy);
        var green = Blend(Green(topLeft), Green(topRight), Green(bottomLeft), Green(bottomRight), fx, fy);
        var blue = Blend(Blue(topLeft), Blue(topRight), Blue(bottomLeft), Blue(bottomRight), fx, fy);
        var alpha = Blend(Alpha(topLeft), Alpha(topRight), Alpha(bottomLeft), Alpha(bottomRight), fx, fy);
        return alpha <= 0 ? 0 : Pack(red, green, blue, alpha);
    }

    private static int Blend(int topLeft, int topRight, int bottomLeft, int bottomRight, double fx, double fy)
    {
        var top = (topLeft * (1.0 - fx)) + (topRight * fx);
        var bottom = (bottomLeft * (1.0 - fx)) + (bottomRight * fx);
        return (int)Math.Round((top * (1.0 - fy)) + (bottom * fy));
    }

    /// <summary>Straight-alpha source over destination.</summary>
    private static int Over(int source, int destination)
    {
        var sourceAlpha = Alpha(source);
        if (sourceAlpha == 0)
        {
            return destination;
        }

        if (sourceAlpha == 255 || Alpha(destination) == 0)
        {
            return source;
        }

        var alpha = sourceAlpha / 255.0;
        var destinationAlpha = Alpha(destination) / 255.0;
        var outAlpha = alpha + (destinationAlpha * (1.0 - alpha));
        if (outAlpha <= 0.0)
        {
            return 0;
        }

        return Pack(
            Mix(Red(source), Red(destination), alpha, destinationAlpha, outAlpha),
            Mix(Green(source), Green(destination), alpha, destinationAlpha, outAlpha),
            Mix(Blue(source), Blue(destination), alpha, destinationAlpha, outAlpha),
            (int)Math.Round(outAlpha * 255.0));
    }

    private static int Mix(int source, int destination, double sourceAlpha, double destinationAlpha, double outAlpha)
        => (int)Math.Round(((source * sourceAlpha) + (destination * destinationAlpha * (1.0 - sourceAlpha))) / outAlpha);

    public static int Pack(int red, int green, int blue, int alpha)
        => (Math.Clamp(alpha, 0, 255) << 24)
           | (Math.Clamp(blue, 0, 255) << 16)
           | (Math.Clamp(green, 0, 255) << 8)
           | Math.Clamp(red, 0, 255);

    public static int Red(int rgba) => rgba & 0xFF;

    public static int Green(int rgba) => (rgba >> 8) & 0xFF;

    public static int Blue(int rgba) => (rgba >> 16) & 0xFF;

    public static int Alpha(int rgba) => (rgba >> 24) & 0xFF;
}
