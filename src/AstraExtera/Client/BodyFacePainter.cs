using AstraExtera.Galaxy;
using AstraTerra.Astronomy;
using Cairo;
using Vintagestory.API.MathTools;

namespace AstraExtera.Client;

/// <summary>
/// Paints the face of a body that is close enough to show a disc: the parent giant a moon world
/// hangs beneath, and the sibling moons crossing that sky.
/// </summary>
/// <remarks>
/// <para>
/// The same appearance the galaxy panel draws as a portrait, painted once at texture size and handed
/// to AstraTerra as pixels. Sending a picture rather than a parameter list is what keeps rings,
/// bands and storms in the mod that authors them: AstraTerra places the body and lights it, and
/// never has to know what a ring division is.
/// </para>
/// <para>
/// The face is painted flat and fully lit. Phase, terminator and limb darkening all belong to the
/// renderer, which shades the disc per vertex from wherever the sun actually is.
/// </para>
/// </remarks>
public static class BodyFacePainter
{
    /// <summary>A parent giant fills a good part of the sky, so its face carries real detail.</summary>
    public const int GiantFaceSize = 512;

    /// <summary>A sibling moon is a few degrees at most; more pixels than this would never be seen.</summary>
    public const int MoonFaceSize = 128;

    public static NearBodyFace PaintGiant(GiantAppearance? appearance, double discFraction)
    {
        var size = GiantFaceSize;
        var fraction = Math.Clamp(discFraction, 0.05, 1.0);
        using var surface = new ImageSurface(Format.Argb32, size, size);
        using var ctx = new Context(surface);

        var centre = size / 2.0;
        var disc = centre * fraction * 0.98;
        var face = appearance ?? DefaultGiantFace();
        var roll = GiantAppearances.RingRollRadians(face);
        var openness = Math.Max(GiantAppearances.RingOpenness(face), 0.015);

        if (face.Ring is { } ring)
        {
            PaintRingHalf(ctx, centre, disc, roll, openness, ring, front: false);
        }

        PaintGlobe(ctx, centre, disc, roll, face);

        if (face.Ring is { } front)
        {
            PaintRingHalf(ctx, centre, disc, roll, openness, front, front: true);
        }

        surface.Flush();
        return new NearBodyFace(size, ToRgba(surface), fraction);
    }

    /// <summary>
    /// A sibling moon: a plain grey world with a few dark basins, which is what a body that never
    /// held an atmosphere looks like.
    /// </summary>
    public static NearBodyFace PaintMoon(SystemMoon moon, long worldSeed)
    {
        ArgumentNullException.ThrowIfNull(moon);
        var size = MoonFaceSize;
        using var surface = new ImageSurface(Format.Argb32, size, size);
        using var ctx = new Context(surface);

        var rng = new SplitMix64(worldSeed + (moon.Index * 7919L));
        var centre = size / 2.0;
        var disc = centre * 0.98;
        var grey = rng.NextRange(0.62, 0.84);

        ctx.Save();
        ctx.NewPath();
        ctx.Arc(centre, centre, disc, 0, 2.0 * Math.PI);
        ctx.Clip();
        SetColor(ctx, grey, grey * 0.98, grey * 0.94);
        ctx.Paint();

        var basins = rng.NextInt(3, 8);
        for (var i = 0; i < basins; i++)
        {
            var angle = rng.NextRange(0.0, 2.0 * Math.PI);
            var distance = rng.NextRange(0.0, disc * 0.85);
            var radius = disc * rng.NextRange(0.08, 0.30);
            var shade = grey * rng.NextRange(0.62, 0.86);
            ctx.NewPath();
            ctx.Arc(centre + (Math.Cos(angle) * distance), centre + (Math.Sin(angle) * distance), radius, 0, 2.0 * Math.PI);
            SetColor(ctx, shade, shade * 0.99, shade * 0.96);
            ctx.Fill();
        }

        ctx.Restore();
        surface.Flush();
        return new NearBodyFace(size, ToRgba(surface), DiscFraction: 1.0);
    }

    private static void PaintGlobe(Context ctx, double centre, double disc, double roll, GiantAppearance face)
    {
        ctx.Save();
        ctx.Translate(centre, centre);
        ctx.Rotate(roll);
        ctx.NewPath();
        ctx.Arc(0, 0, disc, 0, 2.0 * Math.PI);
        ctx.Clip();

        SetColor(ctx, face.BandLightR, face.BandLightG, face.BandLightB);
        ctx.Paint();

        foreach (var band in PlanetPortraits.Bands(face))
        {
            if (band.Light)
            {
                SetColor(ctx, face.BandLightR, face.BandLightG, face.BandLightB);
            }
            else
            {
                SetColor(ctx, face.BandDarkR, face.BandDarkG, face.BandDarkB);
            }

            ctx.Rectangle(-disc, band.Top * disc, disc * 2.0, (band.Bottom - band.Top) * disc);
            ctx.Fill();
        }

        if (face.Storm is { } storm)
        {
            var spot = PlanetPortraits.StormPlacement(storm);
            ctx.Save();
            ctx.Translate(spot.X * disc, spot.Y * disc);
            ctx.Scale(1.0, Math.Max(0.05, spot.RadiusY / spot.RadiusX));
            ctx.NewPath();
            ctx.Arc(0, 0, spot.RadiusX * disc, 0, 2.0 * Math.PI);
            ctx.Restore();
            SetColor(ctx, storm.TintR, storm.TintG, storm.TintB);
            ctx.Fill();
        }

        ctx.Restore();
    }

    private static void PaintRingHalf(
        Context ctx,
        double centre,
        double disc,
        double roll,
        double openness,
        PlanetRing ring,
        bool front)
    {
        var width = Math.Max(1.0, (ring.OuterRadiusPlanetRadii - ring.InnerRadiusPlanetRadii) * disc);
        var mid = (ring.OuterRadiusPlanetRadii + ring.InnerRadiusPlanetRadii) * 0.5 * disc;
        var alpha = Math.Clamp(0.30 + (ring.OpticalDepth * 0.68), 0.12, 0.98);

        RingBand(ctx, centre, roll, openness, mid, width, ring.TintR, ring.TintG, ring.TintB, alpha, front);

        if (ring.HasDivision)
        {
            RingBand(
                ctx,
                centre,
                roll,
                openness,
                ring.DivisionRadiusPlanetRadii * disc,
                Math.Max(1.0, width * 0.18),
                0f,
                0f,
                0f,
                0.0,
                front);
        }
    }

    /// <summary>
    /// One half of the ring, stroked under the squashed transform so its thickness is foreshortened
    /// with the rest of it. The division is stroked as fully transparent, cutting the gap out of the
    /// band that is already there rather than painting a dark line over it.
    /// </summary>
    private static void RingBand(
        Context ctx,
        double centre,
        double roll,
        double openness,
        double radius,
        double width,
        double red,
        double green,
        double blue,
        double alpha,
        bool front)
    {
        ctx.Save();
        ctx.Translate(centre, centre);
        ctx.Rotate(roll);
        ctx.Scale(1.0, openness);
        ctx.NewPath();
        ctx.Arc(0, 0, radius, front ? 0.0 : Math.PI, front ? Math.PI : 2.0 * Math.PI);
        ctx.LineWidth = width;
        ctx.Operator = alpha <= 0.0 ? Operator.Clear : Operator.Over;
        ctx.SetSourceRGBA(red, green, blue, alpha);
        ctx.Stroke();
        ctx.Operator = Operator.Over;
        ctx.Restore();
    }

    private static GiantAppearance DefaultGiantFace()
        => new(
            ObliquityDeg: 12.0,
            Retrograde: false,
            RotationPeriodHours: 10.0,
            AscendingNodeDeg: 90.0,
            BandCount: 9,
            0.94f,
            0.87f,
            0.72f,
            0.62f,
            0.40f,
            0.24f,
            Storm: null,
            Ring: null);

    private static void SetColor(Context ctx, double red, double green, double blue)
        => ctx.SetSourceRGBA(red, green, blue, 1.0);

    /// <summary>
    /// Cairo keeps ARGB32 premultiplied and little-endian; the game wants straight RGBA. Dividing
    /// the colour back out matters at the antialiased rim and through a partly transparent ring,
    /// which is exactly where a face is judged.
    /// </summary>
    private static int[] ToRgba(ImageSurface surface)
    {
        var width = surface.Width;
        var height = surface.Height;
        var stride = surface.Stride;
        var data = surface.Data;
        var pixels = new int[width * height];

        for (var y = 0; y < height; y++)
        {
            var row = y * stride;
            for (var x = 0; x < width; x++)
            {
                var i = row + (x * 4);
                int blue = data[i];
                int green = data[i + 1];
                int red = data[i + 2];
                int alpha = data[i + 3];
                if (alpha is > 0 and < 255)
                {
                    red = Math.Min(255, red * 255 / alpha);
                    green = Math.Min(255, green * 255 / alpha);
                    blue = Math.Min(255, blue * 255 / alpha);
                }

                pixels[(y * width) + x] = ColorUtil.ColorFromRgba(red, green, blue, alpha);
            }
        }

        return pixels;
    }
}
