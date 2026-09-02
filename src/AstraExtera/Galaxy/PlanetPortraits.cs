using System.Globalization;

namespace AstraExtera.Galaxy;

/// <summary>One band across a giant's disc, in units of the disc radius, top edge first.</summary>
public readonly record struct PortraitBand(double Top, double Bottom, bool Light);

/// <summary>A storm on a giant's disc, in units of the disc radius from the disc centre.</summary>
public readonly record struct PortraitStorm(double X, double Y, double RadiusX, double RadiusY);

/// <summary>
/// One planet drawn at portrait size: where its disc sits in the strip, how big it reads, and the
/// ellipse its rings trace around it.
/// </summary>
/// <param name="RingOpenness">
/// 0 when the rings are seen edge-on and 1 when they are wide open. It is the ellipse's minor axis
/// as a fraction of its major axis, so an untilted giant draws its rings as a line.
/// </param>
/// <param name="RingRollRad">Which way the ring line runs across the page.</param>
public sealed record PlanetPortrait(
    CompanionPlanet Body,
    string Label,
    string SizeLabel,
    double Cx,
    double Cy,
    double DiscPx,
    double RingInnerPx,
    double RingOuterPx,
    double RingDivisionPx,
    double RingOpenness,
    double RingRollRad,
    double MoonSpacingPx,
    int MoonCount)
{
    public bool HasRing => Body.Ring is not null && RingOuterPx > RingInnerPx;

    public GiantAppearance? Appearance => Body.Appearance;
}

/// <summary>
/// Lays out the portrait strip: every companion drawn as a disc, with its banding, its long-lived
/// storm, its rings and its moons.
/// <para>
/// The system figures answer "where is everything"; nothing in them can carry a ring's tilt or the
/// colour of a storm, because at that scale a giant is four pixels wide. This is the other half of
/// the answer, and it is laid out once here so the preview page's SVG and the in-game panel's Cairo
/// drawing cannot drift apart.
/// </para>
/// </summary>
public static class PlanetPortraits
{
    public const double ViewWidth = 520.0;
    public const double ViewHeight = 96.0;

    /// <summary>Radius of the largest disc in the strip; everything else is scaled against it.</summary>
    public const double MaxDiscPx = 20.0;

    public const double MinDiscPx = 3.0;

    /// <summary>Moons past this many are left off the strip rather than crowding the disc.</summary>
    public const int MaxDrawnMoons = 5;

    /// <summary>Room kept under each disc for its two label lines.</summary>
    public const double LabelReservePx = 32.0;

    /// <summary>Room kept above each disc for the row of moons.</summary>
    public const double MoonReservePx = 9.0;

    public static IReadOnlyList<PlanetPortrait> Layout(LocalSystem system, double viewWidth = ViewWidth)
    {
        ArgumentNullException.ThrowIfNull(system);
        var bodies = system.Companions;
        if (bodies.Length == 0)
        {
            return [];
        }

        var largest = bodies.Max(static body => body.RadiusEarth);
        var slotWidth = viewWidth / bodies.Length;
        var centreY = ViewHeight * 0.44;

        var discs = new double[bodies.Length];
        for (var i = 0; i < bodies.Length; i++)
        {
            discs[i] = DiscPx(bodies[i].RadiusEarth, largest);
        }

        // Rings reach several planet radii out, so a ringed giant drawn at its own scale would run
        // into its neighbour and over its label. One shrink is applied to the whole strip rather
        // than to the offender alone, because the point of the strip is that the discs compare.
        var shrink = 1.0;
        for (var i = 0; i < bodies.Length; i++)
        {
            var reach = Reach(bodies[i]);
            var halfWidth = discs[i] * reach.Width;
            var halfHeight = discs[i] * reach.Height;
            shrink = Math.Min(shrink, ((slotWidth * 0.5) - 3.0) / Math.Max(halfWidth, 1e-6));
            shrink = Math.Min(shrink, (ViewHeight - centreY - LabelReservePx) / Math.Max(halfHeight, 1e-6));
            shrink = Math.Min(shrink, (centreY - MoonReservePx) / Math.Max(halfHeight, 1e-6));
        }

        shrink = Math.Clamp(shrink, 0.05, 1.0);

        var portraits = new List<PlanetPortrait>(bodies.Length);
        for (var i = 0; i < bodies.Length; i++)
        {
            var body = bodies[i];
            var disc = Math.Max(MinDiscPx * 0.5, discs[i] * shrink);
            var ring = body.Ring;
            var openness = body.Appearance is { } appearance
                ? GiantAppearances.RingOpenness(appearance)
                : 0.0;

            portraits.Add(new PlanetPortrait(
                body,
                LocalSystemGeometry.CompanionLabel(body.Role),
                SizeLabel(body.RadiusEarth),
                Cx: (slotWidth * i) + (slotWidth * 0.5),
                Cy: centreY,
                disc,
                RingInnerPx: ring is null ? 0.0 : disc * ring.InnerRadiusPlanetRadii,
                RingOuterPx: ring is null ? 0.0 : disc * ring.OuterRadiusPlanetRadii,
                RingDivisionPx: ring is { HasDivision: true } ? disc * ring.DivisionRadiusPlanetRadii : 0.0,
                RingOpenness: openness,
                RingRollRad: body.Appearance is { } tilt ? GiantAppearances.RingRollRadians(tilt) : 0.0,
                MoonSpacingPx: 5.0,
                MoonCount: Math.Min(body.Moons.Length, MaxDrawnMoons)));
        }

        return portraits;
    }

    /// <summary>
    /// The half-width and half-height a body needs, in units of its own disc radius. A ring is an
    /// ellipse rolled to the angle its line runs, so a nearly edge-on ring standing on end needs
    /// height where a flat one needs width.
    /// </summary>
    private static (double Width, double Height) Reach(CompanionPlanet body)
    {
        if (body.Appearance is not { Ring: { } ring } appearance)
        {
            return (1.0, 1.0);
        }

        var major = ring.OuterRadiusPlanetRadii;
        var minor = major * GiantAppearances.RingOpenness(appearance);
        var roll = GiantAppearances.RingRollRadians(appearance);
        var cos = Math.Cos(roll);
        var sin = Math.Sin(roll);
        var width = Math.Sqrt((major * cos * major * cos) + (minor * sin * minor * sin));
        var height = Math.Sqrt((major * sin * major * sin) + (minor * cos * minor * cos));
        return (Math.Max(1.0, width), Math.Max(1.0, height));
    }

    /// <summary>
    /// The bands across a giant's disc, alternating light and dark from the north pole down. Equal
    /// slices of the disc rather than of latitude, which is what a sphere's limb darkening makes
    /// them look like anyway.
    /// </summary>
    public static IReadOnlyList<PortraitBand> Bands(GiantAppearance appearance)
    {
        ArgumentNullException.ThrowIfNull(appearance);
        var count = Math.Max(3, appearance.BandCount);
        var bands = new List<PortraitBand>(count);
        for (var i = 0; i < count; i++)
        {
            var top = -1.0 + (2.0 * i / count);
            var bottom = -1.0 + (2.0 * (i + 1) / count);
            bands.Add(new PortraitBand(top, bottom, i % 2 == 0));
        }

        return bands;
    }

    /// <summary>Where a storm sits on the disc, in units of the disc radius.</summary>
    public static PortraitStorm StormPlacement(PlanetStorm storm)
    {
        ArgumentNullException.ThrowIfNull(storm);
        var latitude = storm.LatitudeDeg * Math.PI / 180.0;
        var y = -Math.Sin(latitude);

        // Foreshortened toward the limb, the way a spot near a pole is.
        var cosLatitude = Math.Max(0.15, Math.Cos(latitude));
        var rx = Math.Min(0.42, storm.LongitudeSpanDeg / 360.0 * 2.0) * cosLatitude;
        var ry = Math.Min(0.26, storm.LatitudeSpanDeg / 180.0 * 2.0);
        var x = 0.22 * cosLatitude;
        return new PortraitStorm(x, y, Math.Max(0.05, rx), Math.Max(0.03, ry));
    }

    /// <summary>
    /// How far a portrait reaches to either side of its centre: the ring ellipse as it is actually
    /// drawn, which is the half-width the layout keeps inside the body's own slot.
    /// </summary>
    /// <remarks>
    /// A ring rolled on end is a tall sliver rather than a wide one, so its outer radius is not what
    /// it takes up across the page. Measuring the projected ellipse is what
    /// <see cref="Layout(LocalSystem, double)"/> shrinks against, and this is the same measurement
    /// taken from a laid-out portrait.
    /// </remarks>
    public static double RingHalfWidth(PlanetPortrait portrait)
    {
        ArgumentNullException.ThrowIfNull(portrait);
        if (!portrait.HasRing)
        {
            return portrait.DiscPx;
        }

        var major = portrait.RingOuterPx;
        var minor = major * portrait.RingOpenness;
        var sin = Math.Sin(portrait.RingRollRad);
        var cos = Math.Cos(portrait.RingRollRad);
        var halfWidth = Math.Sqrt((major * cos * major * cos) + (minor * sin * minor * sin));
        return Math.Max(portrait.DiscPx, halfWidth);
    }

    /// <summary>
    /// How far a ring reaches above and below the disc, so labels and moons clear it. A ring rolled
    /// on end reaches as far up the page as it does across it.
    /// </summary>
    public static double RingRise(PlanetPortrait portrait)
    {
        ArgumentNullException.ThrowIfNull(portrait);
        if (!portrait.HasRing)
        {
            return 0.0;
        }

        var major = portrait.RingOuterPx;
        var minor = major * portrait.RingOpenness;
        var sin = Math.Sin(portrait.RingRollRad);
        var cos = Math.Cos(portrait.RingRollRad);
        var halfHeight = Math.Sqrt((major * sin * major * sin) + (minor * cos * minor * cos));
        return Math.Max(0.0, halfHeight - portrait.DiscPx);
    }

    /// <summary>
    /// A body's radius written the way the fact rows write it, Earth symbol and all. Both renderers
    /// can draw that symbol, so the strip and the facts agree.
    /// </summary>
    public static string SizeLabel(double radiusEarth)
        => radiusEarth.ToString(radiusEarth >= 10.0 ? "0.0" : "0.00", CultureInfo.InvariantCulture)
           + " R" + GalaxyFacts.Earth;

    /// <summary>Colour of a body with no authored appearance, so rocky worlds still read.</summary>
    public static (float R, float G, float B) RockyTint()
        => (0.63f, 0.38f, 0.16f);

    public static string Hex(float r, float g, float b)
        => $"#{Channel(r):x2}{Channel(g):x2}{Channel(b):x2}";

    public static uint Rgb(float r, float g, float b)
        => ((uint)Channel(r) << 16) | ((uint)Channel(g) << 8) | (uint)Channel(b);

    /// <summary>The same colour, darkened, for the shaded limb of a disc.</summary>
    public static (float R, float G, float B) Shade(float r, float g, float b, double factor)
        => ((float)(r * factor), (float)(g * factor), (float)(b * factor));

    private static double DiscPx(double radiusEarth, double largestRadiusEarth)
    {
        if (largestRadiusEarth <= 0.0)
        {
            return MinDiscPx;
        }

        var scaled = MaxDiscPx * Math.Pow(Math.Max(0.05, radiusEarth) / largestRadiusEarth, 0.5);
        return Math.Clamp(scaled, MinDiscPx, MaxDiscPx);
    }

    private static int Channel(float value)
        => (int)Math.Round(Math.Clamp(value, 0f, 1f) * 255.0);
}
