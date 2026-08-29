namespace AstraExtera.Galaxy;

public sealed record StarFieldOptions
{
    /// <summary>Faintest apparent magnitude a dark-adapted eye resolves. Earth's naked eye is ~6.5.</summary>
    public double LimitingMagnitude { get; init; } = 6.5;

    /// <summary>
    /// Upper bound on stars kept for rendering; the rest stay unresolved glow. Set above Earth's
    /// ~9,100 naked-eye stars so no world reads as star-poor next to the sky people know, while
    /// still capping the crowded inner-disk sites that would otherwise ask for two or three times
    /// that. AstraTerra reprojects the catalog only when the sky has turned, not every frame.
    /// </summary>
    public int ResolvedStarBudget { get; init; } = 10000;

    public int DirectionCount { get; init; } = 192;

    public int RadialCellCount { get; init; } = 72;

    public double NearestDistancePc { get; init; } = 0.5;

    public double FarthestDistancePc { get; init; } = 30000.0;
}

public sealed record VisibleStar(
    double GalacticLongitudeRad,
    double GalacticLatitudeRad,
    double DistancePc,
    double AbsoluteMagnitude,
    double ApparentMagnitude,
    double ExtinctionMagnitudes,
    double ColorIndexBv);

public sealed record StarField(
    IReadOnlyList<VisibleStar> Stars,
    double ExpectedVisibleCount,
    int SampledCount,
    double LimitingMagnitude,
    bool Truncated)
{
    public double UnresolvedCount => Math.Max(0.0, ExpectedVisibleCount - Stars.Count);

    /// <summary>
    /// The faintest star actually kept. Where the render budget bites, this sits brighter than the
    /// requested limit, so a crowded vantage point resolves fewer magnitudes rather than more stars.
    /// </summary>
    public double EffectiveLimitingMagnitude
        => Stars.Count == 0 ? LimitingMagnitude : Stars[^1].ApparentMagnitude;
}

/// <summary>
/// Draws the stars an observer on the authored world would actually see.
/// <para>
/// Nothing here picks a star count. For every absolute-magnitude bin of the luminosity function
/// the sampler marches outward along many sight lines, accumulating dust extinction, and stops
/// where a star of that brightness would fall below the eye's limit. The number of visible stars
/// is the luminosity function integrated against the local stellar density inside those horizons,
/// so a dense inner-disk world sees far more stars than an outer-disk or elliptical-halo world,
/// and a dusty midplane world loses the distant ones.
/// </para>
/// <para>
/// The live game samples once on the server and stores the result. This type is the authoring
/// step and the preview tool; clients render the stored <see cref="StarField"/> instead of
/// calling <see cref="Sample"/>.
/// </para>
/// </summary>
public static class StarFieldSampler
{
    /// <summary>Visual extinction per kiloparsec per unit dust density; ~1 mag/kpc in a solar-analog midplane.</summary>
    public const double ExtinctionMagPerKpcPerDust = 0.29;

    public static StarField Sample(GalaxyPlacement placement, StarFieldOptions? options = null)
    {
        var settings = options ?? new StarFieldOptions();
        var galaxy = placement.Galaxy;
        var frame = new ObserverFrame(placement.Location);
        var rng = new SplitMix64(MixSeed(placement.WorldSeed, 0x57A45));

        var radii = BuildRadialCells(settings);
        var sightLines = BuildSightLines(galaxy, frame, settings, radii);

        var stars = new List<VisibleStar>();
        var expectedTotal = 0.0;

        for (var bin = 0; bin < StellarLuminosityFunction.BinCount; bin++)
        {
            var absoluteMagnitude = StellarLuminosityFunction.BinAbsoluteMagnitude(bin);
            var densityPerPc3 = StellarLuminosityFunction.BinDensity(bin) * StellarLuminosityFunction.BinWidth;
            var reach = settings.LimitingMagnitude - absoluteMagnitude;

            var perLineWeight = new double[sightLines.Length];
            var horizon = new int[sightLines.Length];
            var binWeight = 0.0;
            for (var line = 0; line < sightLines.Length; line++)
            {
                var visibleCells = sightLines[line].HorizonCellCount(reach);
                horizon[line] = visibleCells;
                binWeight += sightLines[line].WeightThrough(visibleCells);
                perLineWeight[line] = binWeight;
            }

            var expected = densityPerPc3 * binWeight;
            expectedTotal += expected;
            if (expected <= 0.0)
            {
                continue;
            }

            var count = rng.NextPoisson(expected);
            for (var i = 0; i < count; i++)
            {
                var line = PickWeighted(perLineWeight, rng.NextUnit() * binWeight);
                var star = SampleStar(
                    sightLines[line],
                    horizon[line],
                    absoluteMagnitude,
                    settings,
                    ref rng);
                if (star is not null)
                {
                    stars.Add(star);
                }
            }
        }

        // A total order, not just brightness: catalog ids are handed out by position in this list
        // and player-drawn constellations reference those ids, so ties must not reorder.
        stars.Sort(static (left, right) =>
        {
            var byMagnitude = left.ApparentMagnitude.CompareTo(right.ApparentMagnitude);
            if (byMagnitude != 0)
            {
                return byMagnitude;
            }

            var byLongitude = left.GalacticLongitudeRad.CompareTo(right.GalacticLongitudeRad);
            return byLongitude != 0
                ? byLongitude
                : left.GalacticLatitudeRad.CompareTo(right.GalacticLatitudeRad);
        });
        var sampled = stars.Count;
        var truncated = sampled > settings.ResolvedStarBudget;
        if (truncated)
        {
            stars.RemoveRange(settings.ResolvedStarBudget, sampled - settings.ResolvedStarBudget);
        }

        return new StarField(stars, expectedTotal, sampled, settings.LimitingMagnitude, truncated);
    }

    private static VisibleStar? SampleStar(
        SightLine line,
        int horizonCells,
        double absoluteMagnitude,
        StarFieldOptions settings,
        ref SplitMix64 rng)
    {
        if (horizonCells <= 0)
        {
            return null;
        }

        var cell = line.PickCell(horizonCells, rng.NextUnit());
        var distancePc = line.JitterDistancePc(cell, rng.NextUnit());
        var extinction = line.ExtinctionAt(cell);
        var apparentMagnitude = absoluteMagnitude + 5.0 * Math.Log10(distancePc / 10.0) + extinction;
        if (apparentMagnitude > settings.LimitingMagnitude)
        {
            return null;
        }

        var (longitude, latitude) = line.JitterDirection(rng.NextUnit(), rng.NextUnit());
        return new VisibleStar(
            longitude,
            latitude,
            distancePc,
            absoluteMagnitude,
            apparentMagnitude,
            extinction,
            ColorIndex(absoluteMagnitude, ref rng));
    }

    /// <summary>
    /// Rough main-sequence colour with a giant branch: above the turnoff a star is either a hot
    /// blue supergiant or an evolved red giant, which is what makes bright stars visibly two-toned.
    /// </summary>
    private static double ColorIndex(double absoluteMagnitude, ref SplitMix64 rng)
    {
        double baseColor;
        if (absoluteMagnitude < 1.0)
        {
            baseColor = rng.NextBool(0.45) ? 1.45 : -0.12;
        }
        else
        {
            baseColor = absoluteMagnitude switch
            {
                < 2.0 => 0.05,
                < 3.0 => 0.25,
                < 4.0 => 0.45,
                < 5.0 => 0.62,
                < 6.0 => 0.75,
                < 7.0 => 0.92,
                < 9.0 => 1.20,
                _ => 1.50
            };
        }

        return Math.Clamp(baseColor + rng.NextGaussian(0.0, 0.06), -0.35, 2.0);
    }

    private static double[] BuildRadialCells(StarFieldOptions settings)
    {
        var edges = new double[settings.RadialCellCount + 1];
        var logNear = Math.Log(settings.NearestDistancePc);
        var logFar = Math.Log(settings.FarthestDistancePc);
        for (var i = 0; i <= settings.RadialCellCount; i++)
        {
            edges[i] = Math.Exp(logNear + (logFar - logNear) * i / settings.RadialCellCount);
        }

        return edges;
    }

    private static SightLine[] BuildSightLines(
        GalaxyBlueprint galaxy,
        ObserverFrame frame,
        StarFieldOptions settings,
        double[] radialEdges)
    {
        var lines = new SightLine[settings.DirectionCount];
        var solidAngle = 4.0 * Math.PI / settings.DirectionCount;
        var golden = Math.PI * (3.0 - Math.Sqrt(5.0));
        var cellCount = settings.RadialCellCount;

        for (var i = 0; i < settings.DirectionCount; i++)
        {
            var latitude = Math.Asin(1.0 - 2.0 * (i + 0.5) / settings.DirectionCount);
            var longitude = NormalizeLongitude(golden * i);
            var direction = frame.Direction(longitude, latitude);

            var cumulativeWeight = new double[cellCount + 1];
            var extinction = new double[cellCount];
            var distanceModulusPlusExtinction = new double[cellCount];
            var accumulatedExtinction = 0.0;

            for (var cell = 0; cell < cellCount; cell++)
            {
                var innerPc = radialEdges[cell];
                var outerPc = radialEdges[cell + 1];
                var midPc = 0.5 * (innerPc + outerPc);
                var thicknessPc = outerPc - innerPc;
                var point = frame.PointAt(direction, midPc / 1000.0);

                var stellarDensity = GalaxyGenerator.StellarDensityAt(galaxy, point.X, point.Y, point.Z);
                var dust = GalaxyGenerator.DustDensityAt(galaxy, point.X, point.Y, point.Z);
                accumulatedExtinction += dust * (thicknessPc / 1000.0) * ExtinctionMagPerKpcPerDust;
                extinction[cell] = accumulatedExtinction;
                distanceModulusPlusExtinction[cell] =
                    5.0 * Math.Log10(midPc / 10.0) + accumulatedExtinction;

                var volumePc3 = midPc * midPc * thicknessPc * solidAngle;
                cumulativeWeight[cell + 1] = cumulativeWeight[cell] + stellarDensity * volumePc3;
            }

            lines[i] = new SightLine(
                longitude,
                latitude,
                solidAngle,
                radialEdges,
                cumulativeWeight,
                extinction,
                distanceModulusPlusExtinction);
        }

        return lines;
    }

    private static int PickWeighted(double[] cumulative, double target)
    {
        var low = 0;
        var high = cumulative.Length - 1;
        while (low < high)
        {
            var mid = (low + high) / 2;
            if (cumulative[mid] < target)
            {
                low = mid + 1;
            }
            else
            {
                high = mid;
            }
        }

        return low;
    }

    private static double NormalizeLongitude(double radians)
    {
        var twoPi = 2.0 * Math.PI;
        var wrapped = radians % twoPi;
        if (wrapped > Math.PI)
        {
            wrapped -= twoPi;
        }
        else if (wrapped < -Math.PI)
        {
            wrapped += twoPi;
        }

        return wrapped;
    }

    private static long MixSeed(long worldSeed, long salt)
    {
        unchecked
        {
            var mixed = (ulong)worldSeed ^ ((ulong)salt * 0x9E3779B97F4A7C15UL);
            mixed ^= mixed >> 30;
            mixed *= 0xBF58476D1CE4E5B9UL;
            mixed ^= mixed >> 27;
            mixed *= 0x94D049BB133111EBUL;
            mixed ^= mixed >> 31;
            return (long)mixed;
        }
    }

    /// <summary>
    /// One pencil beam through the galaxy, pre-integrated so every magnitude bin can ask "how far
    /// can I see" and "how much stellar mass is inside that" without re-walking the density field.
    /// </summary>
    private sealed class SightLine
    {
        private readonly double solidAngle;
        private readonly double[] radialEdges;
        private readonly double[] cumulativeWeight;
        private readonly double[] extinction;
        private readonly double[] distanceModulusPlusExtinction;

        public SightLine(
            double longitudeRad,
            double latitudeRad,
            double solidAngle,
            double[] radialEdges,
            double[] cumulativeWeight,
            double[] extinction,
            double[] distanceModulusPlusExtinction)
        {
            LongitudeRad = longitudeRad;
            LatitudeRad = latitudeRad;
            this.solidAngle = solidAngle;
            this.radialEdges = radialEdges;
            this.cumulativeWeight = cumulativeWeight;
            this.extinction = extinction;
            this.distanceModulusPlusExtinction = distanceModulusPlusExtinction;
        }

        public double LongitudeRad { get; }

        public double LatitudeRad { get; }

        /// <summary>
        /// Distance modulus plus extinction both rise with distance, so visibility is a prefix:
        /// the horizon is however many cells stay within the magnitude budget.
        /// </summary>
        public int HorizonCellCount(double magnitudeReach)
        {
            var low = 0;
            var high = distanceModulusPlusExtinction.Length;
            while (low < high)
            {
                var mid = (low + high) / 2;
                if (distanceModulusPlusExtinction[mid] <= magnitudeReach)
                {
                    low = mid + 1;
                }
                else
                {
                    high = mid;
                }
            }

            return low;
        }

        public double WeightThrough(int cellCount) => cumulativeWeight[cellCount];

        public int PickCell(int horizonCells, double unit)
        {
            var target = unit * cumulativeWeight[horizonCells];
            var low = 0;
            var high = horizonCells - 1;
            while (low < high)
            {
                var mid = (low + high) / 2;
                if (cumulativeWeight[mid + 1] < target)
                {
                    low = mid + 1;
                }
                else
                {
                    high = mid;
                }
            }

            return low;
        }

        public double JitterDistancePc(int cell, double unit)
            => radialEdges[cell] + (radialEdges[cell + 1] - radialEdges[cell]) * unit;

        public double ExtinctionAt(int cell) => extinction[cell];

        /// <summary>Spreads the star across the patch this beam represents so the sky is not a lattice.</summary>
        public (double LongitudeRad, double LatitudeRad) JitterDirection(double unitA, double unitB)
        {
            var patchRadius = Math.Sqrt(solidAngle / Math.PI);
            var offset = patchRadius * Math.Sqrt(unitA);
            var angle = 2.0 * Math.PI * unitB;
            var latitude = Math.Clamp(
                LatitudeRad + offset * Math.Sin(angle),
                -Math.PI / 2.0 + 1e-6,
                Math.PI / 2.0 - 1e-6);
            var longitude = NormalizeLongitude(
                LongitudeRad + offset * Math.Cos(angle) / Math.Max(0.15, Math.Cos(latitude)));
            return (longitude, latitude);
        }
    }
}
