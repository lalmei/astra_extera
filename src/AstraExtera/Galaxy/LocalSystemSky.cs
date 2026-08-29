namespace AstraExtera.Galaxy;

/// <summary>
/// Keplerian elements in the observer's orbital plane, which AstraTerra treats as its ecliptic.
/// Rates other than mean longitude stay zero: these orbits are authored, not integrated.
/// </summary>
public sealed record AuthoredOrbit(
    double SemiMajorAxisAu,
    double Eccentricity,
    double InclinationDeg,
    double MeanLongitudeDeg,
    double MeanLongitudeRateDegPerCentury,
    double LongitudeOfPerihelionDeg,
    double LongitudeOfAscendingNodeDeg);

public sealed record AuthoredPlanet(
    string Id,
    string DisplayName,
    CompanionRole Role,
    AuthoredOrbit Orbit,
    double AbsoluteMagnitude,
    double PhaseCoefficient,
    float TintR,
    float TintG,
    float TintB);

public sealed record AuthoredCometPathKeyframe(
    double Phase,
    double RightAscensionDeg,
    double DeclinationDeg);

public sealed record AuthoredComet(
    string Id,
    string DisplayName,
    double PeriodYears,
    double FirstPerihelionYear,
    double WindowHalfWidthYears,
    double PeakMagnitude,
    double EdgeMagnitude,
    double BrighteningExponent,
    double PeakTailLengthDeg,
    IReadOnlyList<AuthoredCometPathKeyframe> Path,
    float TintR,
    float TintG,
    float TintB);

public sealed record AuthoredMeteorShower(
    string Id,
    string DisplayName,
    string ParentCometId,
    double RightAscensionDeg,
    double DeclinationDeg,
    double PeakSolarLongitudeDeg,
    double WindowHalfWidthDeg,
    double PeakZenithHourlyRate);

/// <summary>
/// The local system's wanderers: companion planets on Keplerian tracks, leftover comets scattered
/// by the shepherd giant, and the meteor showers those comets leave on the observer's orbit.
/// <para>
/// Authored once on the server from the stored placement. Companion planets are the bodies already
/// drawn in the system diagram; sibling moons of a habitable moon stay off the planet catalog,
/// because AstraTerra's ephemeris is heliocentric and those moons would collapse onto the parent.
/// </para>
/// </summary>
public sealed record LocalSystemSky(
    AuthoredOrbit Observer,
    IReadOnlyList<AuthoredPlanet> Planets,
    IReadOnlyList<AuthoredComet> Comets,
    IReadOnlyList<AuthoredMeteorShower> Showers)
{
    public const int SchemaVersion = 1;

    /// <summary>Julian years per century, matching the published planetary element rates.</summary>
    public const double YearsPerCentury = 100.0;

    public static LocalSystemSky Author(GalaxyPlacement placement)
    {
        ArgumentNullException.ThrowIfNull(placement);
        var rng = new SplitMix64(MixSeed(placement.WorldSeed, 0xC0E7));
        var system = placement.System;

        var observer = SampleOrbit(
            ref rng,
            system.OrbitalDistanceAu,
            system.OrbitalPeriodDays,
            eccentricity: rng.NextRange(0.008, 0.040),
            inclinationDeg: 0.0);

        var planets = new List<AuthoredPlanet>(system.Companions.Length);
        var usedNames = new HashSet<string>(StringComparer.Ordinal);
        for (var i = 0; i < system.Companions.Length; i++)
        {
            planets.Add(PlanetFrom(system.Companions[i], i, ref rng, usedNames));
        }

        var cometCount = 2 + rng.NextInt(3);
        var comets = new List<AuthoredComet>(cometCount);
        for (var i = 0; i < cometCount; i++)
        {
            comets.Add(SampleComet(placement, i, ref rng, usedNames));
        }

        var showers = new List<AuthoredMeteorShower>(comets.Count + 2);
        foreach (var comet in comets)
        {
            showers.Add(ShowerFrom(comet, inbound: true, ref rng));
            if (comet.PeriodYears >= 45.0)
            {
                showers.Add(ShowerFrom(comet, inbound: false, ref rng));
            }
        }

        return new LocalSystemSky(observer, planets, comets, showers);
    }

    public static double MeanLongitudeRateDegPerCentury(double orbitalPeriodDays)
        => 360.0 * YearsPerCentury * 365.25 / orbitalPeriodDays;

    private static AuthoredPlanet PlanetFrom(
        CompanionPlanet companion,
        int index,
        ref SplitMix64 rng,
        HashSet<string> usedNames)
    {
        var (minE, maxE, minI, maxI) = companion.Role switch
        {
            CompanionRole.InnerRocky => (0.02, 0.12, 0.4, 6.5),
            CompanionRole.ShepherdGiant => (0.02, 0.08, 0.3, 3.2),
            CompanionRole.OuterIceGiant => (0.01, 0.06, 0.4, 4.5),
            _ => (0.02, 0.08, 0.5, 4.0)
        };

        var orbit = SampleOrbit(
            ref rng,
            companion.SemiMajorAxisAu,
            companion.OrbitalPeriodDays,
            rng.NextRange(minE, maxE),
            rng.NextRange(minI, maxI));

        var (tintR, tintG, tintB) = TintFor(companion.Role, ref rng);
        return new AuthoredPlanet(
            Id: $"companion-{index + 1}-{companion.Role.ToString().ToLowerInvariant()}",
            DisplayName: PickName(NamesFor(companion.Role), ref rng, usedNames),
            companion.Role,
            orbit,
            AbsoluteMagnitude: AbsoluteMagnitudeFor(companion),
            PhaseCoefficient: companion.Role == CompanionRole.InnerRocky ? 0.024 : 0.007,
            tintR,
            tintG,
            tintB);
    }

    private static AuthoredOrbit SampleOrbit(
        ref SplitMix64 rng,
        double semiMajorAxisAu,
        double orbitalPeriodDays,
        double eccentricity,
        double inclinationDeg)
        => new(
            semiMajorAxisAu,
            eccentricity,
            inclinationDeg,
            MeanLongitudeDeg: rng.NextRange(0.0, 360.0),
            MeanLongitudeRateDegPerCentury: MeanLongitudeRateDegPerCentury(orbitalPeriodDays),
            LongitudeOfPerihelionDeg: rng.NextRange(0.0, 360.0),
            LongitudeOfAscendingNodeDeg: rng.NextRange(0.0, 360.0));

    private static AuthoredComet SampleComet(
        GalaxyPlacement placement,
        int index,
        ref SplitMix64 rng,
        HashSet<string> usedNames)
    {
        var roll = rng.NextUnit();
        double periodYears;
        double peakMagnitude;
        double peakTailLengthDeg;
        double brightening;
        if (roll < 0.50)
        {
            periodYears = rng.NextRange(4.6, 12.5);
            peakMagnitude = rng.NextRange(3.2, 5.2);
            peakTailLengthDeg = rng.NextRange(4.0, 8.0);
            brightening = rng.NextRange(0.65, 0.80);
        }
        else if (roll < 0.80)
        {
            periodYears = rng.NextRange(16.0, 38.0);
            peakMagnitude = rng.NextRange(1.5, 3.8);
            peakTailLengthDeg = rng.NextRange(7.0, 14.0);
            brightening = rng.NextRange(0.55, 0.70);
        }
        else
        {
            periodYears = rng.NextRange(48.0, 88.0);
            peakMagnitude = rng.NextRange(-1.2, 2.2);
            peakTailLengthDeg = rng.NextRange(14.0, 24.0);
            brightening = rng.NextRange(0.48, 0.62);
        }

        var name = PickName(CometNames, ref rng, usedNames);
        var firstPerihelion = rng.NextRange(0.35, periodYears);
        var sweepDeg = rng.NextRange(70.0, 115.0);
        var path = BuildPath(placement.Orientation, sweepDeg, ref rng);
        var cool = (float)rng.NextRange(0.72, 0.90);
        return new AuthoredComet(
            Id: $"comet-{index + 1}",
            DisplayName: name,
            periodYears,
            firstPerihelion,
            WindowHalfWidthYears: rng.NextRange(0.035, 0.080),
            peakMagnitude,
            EdgeMagnitude: peakMagnitude + rng.NextRange(4.5, 6.5),
            brightening,
            peakTailLengthDeg,
            path,
            TintR: cool,
            TintG: (float)rng.NextRange(0.90, 1.0),
            TintB: 1f);
    }

    private static IReadOnlyList<AuthoredCometPathKeyframe> BuildPath(
        CelestialOrientation orientation,
        double sweepDeg,
        ref SplitMix64 rng)
    {
        var start = RandomGalacticDirection(ref rng);
        var axis = RandomGalacticDirection(ref rng);
        var end = RotateToward(start, axis, sweepDeg * Math.PI / 180.0);
        var phases = new[] { -1.0, -0.45, -0.12, 0.0, 0.12, 0.45, 1.0 };
        var keyframes = new AuthoredCometPathKeyframe[phases.Length];
        for (var i = 0; i < phases.Length; i++)
        {
            var t = (phases[i] + 1.0) / 2.0;
            var direction = Slerp(start, end, t);
            var longitude = Math.Atan2(direction.Y, direction.X);
            var latitude = Math.Asin(Math.Clamp(direction.Z, -1.0, 1.0));
            var (rightAscension, declination) = orientation.ToEquatorial(longitude, latitude);
            keyframes[i] = new AuthoredCometPathKeyframe(
                phases[i],
                WrapDegrees(rightAscension),
                declination);
        }

        return keyframes;
    }

    private static AuthoredMeteorShower ShowerFrom(AuthoredComet comet, bool inbound, ref SplitMix64 rng)
    {
        var keyframe = inbound ? comet.Path[0] : comet.Path[^1];
        var perihelionFraction = comet.FirstPerihelionYear - Math.Floor(comet.FirstPerihelionYear);
        var peakSolar = WrapDegrees((perihelionFraction * 360.0) + (inbound ? 0.0 : 180.0));
        var zhr = Math.Clamp(10.0 + ((6.0 - comet.PeakMagnitude) * 10.0), 8.0, 90.0);
        var suffix = inbound ? "ids" : " return";
        return new AuthoredMeteorShower(
            Id: inbound ? $"{comet.Id}-in" : $"{comet.Id}-out",
            DisplayName: comet.DisplayName + suffix,
            comet.Id,
            keyframe.RightAscensionDeg,
            keyframe.DeclinationDeg,
            peakSolar,
            WindowHalfWidthDeg: rng.NextRange(7.0, 18.0),
            PeakZenithHourlyRate: zhr);
    }

    private static double AbsoluteMagnitudeFor(CompanionPlanet companion)
        => companion.Role switch
        {
            CompanionRole.InnerRocky => 1.8 - (2.6 * Math.Log10(Math.Max(0.08, companion.RadiusEarth))),
            CompanionRole.ShepherdGiant => -8.2 - (0.004 * (companion.MassEarth - 180.0)),
            CompanionRole.OuterIceGiant => -6.6 - (0.02 * (companion.MassEarth - 17.0)),
            _ => 0.0
        };

    private static (float R, float G, float B) TintFor(CompanionRole role, ref SplitMix64 rng)
        => role switch
        {
            CompanionRole.InnerRocky => (
                (float)rng.NextRange(0.82, 0.96),
                (float)rng.NextRange(0.62, 0.80),
                (float)rng.NextRange(0.48, 0.66)),
            CompanionRole.ShepherdGiant => (
                (float)rng.NextRange(0.88, 1.0),
                (float)rng.NextRange(0.78, 0.92),
                (float)rng.NextRange(0.62, 0.78)),
            CompanionRole.OuterIceGiant => (
                (float)rng.NextRange(0.55, 0.72),
                (float)rng.NextRange(0.72, 0.88),
                (float)rng.NextRange(0.90, 1.0)),
            _ => (0.9f, 0.9f, 0.9f)
        };

    private static string[] NamesFor(CompanionRole role)
        => role switch
        {
            CompanionRole.InnerRocky => InnerNames,
            CompanionRole.ShepherdGiant => ShepherdNames,
            CompanionRole.OuterIceGiant => IceNames,
            _ => InnerNames
        };

    private static string PickName(string[] names, ref SplitMix64 rng, HashSet<string> used)
    {
        for (var attempt = 0; attempt < names.Length * 2; attempt++)
        {
            var name = names[rng.NextInt(names.Length)];
            if (used.Add(name))
            {
                return name;
            }
        }

        var fallback = names[0] + used.Count;
        used.Add(fallback);
        return fallback;
    }

    private static (double X, double Y, double Z) RandomGalacticDirection(ref SplitMix64 rng)
    {
        var latitude = Math.Asin(1.0 - 2.0 * rng.NextUnit());
        var longitude = rng.NextRange(-Math.PI, Math.PI);
        var cosB = Math.Cos(latitude);
        return (cosB * Math.Cos(longitude), cosB * Math.Sin(longitude), Math.Sin(latitude));
    }

    private static (double X, double Y, double Z) RotateToward(
        (double X, double Y, double Z) start,
        (double X, double Y, double Z) axisHint,
        double angleRad)
    {
        var axis = Normalize(Subtract(axisHint, Scale(start, Dot(axisHint, start))));
        if (Length(axis) < 1e-8)
        {
            axis = Math.Abs(start.Z) < 0.9 ? Normalize((-start.Y, start.X, 0.0)) : (1.0, 0.0, 0.0);
        }

        var cos = Math.Cos(angleRad);
        var sin = Math.Sin(angleRad);
        return Normalize(Add(Scale(start, cos), Scale(axis, sin)));
    }

    private static (double X, double Y, double Z) Slerp(
        (double X, double Y, double Z) a,
        (double X, double Y, double Z) b,
        double t)
    {
        var dot = Math.Clamp(Dot(a, b), -1.0, 1.0);
        if (dot > 0.9995)
        {
            return Normalize(Add(Scale(a, 1.0 - t), Scale(b, t)));
        }

        var omega = Math.Acos(dot);
        var sinOmega = Math.Sin(omega);
        return Add(
            Scale(a, Math.Sin((1.0 - t) * omega) / sinOmega),
            Scale(b, Math.Sin(t * omega) / sinOmega));
    }

    private static double WrapDegrees(double degrees)
    {
        var wrapped = degrees % 360.0;
        return wrapped < 0.0 ? wrapped + 360.0 : wrapped;
    }

    private static double Dot((double X, double Y, double Z) a, (double X, double Y, double Z) b)
        => a.X * b.X + a.Y * b.Y + a.Z * b.Z;

    private static double Length((double X, double Y, double Z) a) => Math.Sqrt(Dot(a, a));

    private static (double X, double Y, double Z) Add(
        (double X, double Y, double Z) a,
        (double X, double Y, double Z) b)
        => (a.X + b.X, a.Y + b.Y, a.Z + b.Z);

    private static (double X, double Y, double Z) Subtract(
        (double X, double Y, double Z) a,
        (double X, double Y, double Z) b)
        => (a.X - b.X, a.Y - b.Y, a.Z - b.Z);

    private static (double X, double Y, double Z) Scale((double X, double Y, double Z) a, double factor)
        => (a.X * factor, a.Y * factor, a.Z * factor);

    private static (double X, double Y, double Z) Normalize((double X, double Y, double Z) a)
    {
        var length = Length(a);
        return length < 1e-12 ? (1.0, 0.0, 0.0) : Scale(a, 1.0 / length);
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

    private static readonly string[] InnerNames = ["Cinder", "Ember", "Flint", "Ochre", "Pumice"];
    private static readonly string[] ShepherdNames = ["Warden", "Keeper", "Bulwark", "Titan", "Sable"];
    private static readonly string[] IceNames = ["Rime", "Glaze", "Floe", "Cobalt", "Indigo"];
    private static readonly string[] CometNames =
        ["Vesper", "Harl", "Brume", "Wyrm", "Mote", "Drift", "Harrow", "Calx", "Omen", "Lumen"];
}
