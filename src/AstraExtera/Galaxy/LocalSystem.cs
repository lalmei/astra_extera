using System.Globalization;

namespace AstraExtera.Galaxy;

/// <summary>
/// Host star and inner-system layout for the playable world.
/// <para>
/// The pipeline is the one Historia Extera uses for <c>WorldCosmology</c>: mass–luminosity,
/// habitable-zone placement, albedo/greenhouse energy balance, an outer shepherd giant past
/// the snow line, and, when the world is a moon, a Roche-safe family around a parent giant.
/// The playable body itself stays the Earth analog already sampled for gait and iron; this
/// only finds a star and an orbit that keep that body liquid-water habitable.
/// </para>
/// </summary>
public sealed record LocalSystem(
    StarSpectralClass StarClass,
    double StarMassSolar,
    double StarRadiusSolar,
    double LuminositySolar,
    double StarLifespanGyr,
    double HabitableZoneInnerAu,
    double HabitableZoneOuterAu,
    double OrbitalDistanceAu,
    double OrbitalPeriodDays,
    double BondAlbedo,
    double GreenhouseDeltaK,
    double SnowLineAu,
    double? ParentGiantMassEarth,
    double? MoonOrbitalDistanceEarthRadii,
    double? MoonDayLengthDays,
    double? RocheLimitEarthRadii,
    CompanionPlanet[] Companions,
    SystemMoon[] Moons,
    int? HabitableMoonIndex)
{
    public const double MinStarLifespanGyr = 2.0;
    public const double MaxMoonDayDays = 7.0;
    public const double MinMoonDayDays = 0.40;
    public const double MinHillSeparation = 8.0;

    /// <summary>
    /// Planets closer in than this lock to the star on a geological timescale, which would
    /// freeze Vintage Story's day/night. Moons keep a day from orbiting their parent instead.
    /// </summary>
    public const double MinPlanetYearDays = 40.0;

    internal const double EarthMassesPerSolar = 332_946.0;
    internal const double EarthRadiiPerSolar = 109.2;
    internal const double EarthRadiiPerAu = 23_455.0;

    public const int MaxAttempts = 64;

    public string StarClassLabel => StarClass switch
    {
        StarSpectralClass.M => "M-type",
        StarSpectralClass.K => "K-type",
        StarSpectralClass.G => "G-type",
        StarSpectralClass.F => "F-type",
        _ => StarClass.ToString()
    };

    public bool IsHabitable => Checks.All(static check => check.Passed);

    public IReadOnlyList<LocalSystemCheck> Checks => EvaluateChecks();

    public bool Equals(LocalSystem? other)
    {
        if (other is null)
        {
            return false;
        }

        if (ReferenceEquals(this, other))
        {
            return true;
        }

        return StarClass == other.StarClass
               && StarMassSolar.Equals(other.StarMassSolar)
               && StarRadiusSolar.Equals(other.StarRadiusSolar)
               && LuminositySolar.Equals(other.LuminositySolar)
               && StarLifespanGyr.Equals(other.StarLifespanGyr)
               && HabitableZoneInnerAu.Equals(other.HabitableZoneInnerAu)
               && HabitableZoneOuterAu.Equals(other.HabitableZoneOuterAu)
               && OrbitalDistanceAu.Equals(other.OrbitalDistanceAu)
               && OrbitalPeriodDays.Equals(other.OrbitalPeriodDays)
               && BondAlbedo.Equals(other.BondAlbedo)
               && GreenhouseDeltaK.Equals(other.GreenhouseDeltaK)
               && SnowLineAu.Equals(other.SnowLineAu)
               && ParentGiantMassEarth.Equals(other.ParentGiantMassEarth)
               && MoonOrbitalDistanceEarthRadii.Equals(other.MoonOrbitalDistanceEarthRadii)
               && MoonDayLengthDays.Equals(other.MoonDayLengthDays)
               && RocheLimitEarthRadii.Equals(other.RocheLimitEarthRadii)
               && HabitableMoonIndex.Equals(other.HabitableMoonIndex)
               && Companions.SequenceEqual(other.Companions)
               && Moons.SequenceEqual(other.Moons);
    }

    public override int GetHashCode()
    {
        var hash = new HashCode();
        hash.Add(StarClass);
        hash.Add(StarMassSolar);
        hash.Add(StarRadiusSolar);
        hash.Add(LuminositySolar);
        hash.Add(StarLifespanGyr);
        hash.Add(HabitableZoneInnerAu);
        hash.Add(HabitableZoneOuterAu);
        hash.Add(OrbitalDistanceAu);
        hash.Add(OrbitalPeriodDays);
        hash.Add(BondAlbedo);
        hash.Add(GreenhouseDeltaK);
        hash.Add(SnowLineAu);
        hash.Add(ParentGiantMassEarth);
        hash.Add(MoonOrbitalDistanceEarthRadii);
        hash.Add(MoonDayLengthDays);
        hash.Add(RocheLimitEarthRadii);
        hash.Add(HabitableMoonIndex);
        foreach (var companion in Companions)
        {
            hash.Add(companion);
        }

        foreach (var moon in Moons)
        {
            hash.Add(moon);
        }

        return hash.ToHashCode();
    }

    public static LocalSystem Sample(
        ref SplitMix64 rng,
        ObserverWorldKind kind,
        EarthAnalogWorld bulk,
        out EarthAnalogWorld world)
    {
        ArgumentNullException.ThrowIfNull(bulk);

        for (var attempt = 0; attempt < MaxAttempts; attempt++)
        {
            if (TrySample(ref rng, kind, bulk, out var system, out world))
            {
                return system;
            }
        }

        var fallback = Fallback();
        world = EarthAnalog.WithClimate(
            bulk,
            EarthAnalog.EarthEquilibriumTemperatureK,
            EarthAnalog.EarthSurfaceTemperatureK);
        return fallback;
    }

    public static (double Min, double Max) MassRange(StarSpectralClass starClass) => starClass switch
    {
        StarSpectralClass.M => (0.08, 0.45),
        StarSpectralClass.K => (0.45, 0.80),
        StarSpectralClass.G => (0.80, 1.04),
        StarSpectralClass.F => (1.04, 1.40),
        _ => (0.80, 1.04)
    };

    /// <summary>L_* ≈ M_*^3.5 in solar units.</summary>
    public static double MassLuminosity(double massSolar)
        => Math.Pow(massSolar, 3.0) * Math.Sqrt(massSolar);

    /// <summary>Main-sequence radius R_* ≈ M_*^0.8, in solar radii.</summary>
    public static double ComputeStarRadiusSolar(double massSolar)
        => Math.Pow(Math.Max(0.0, massSolar), 0.8);

    /// <summary>T ≈ 10 × M_*^-2.5 billion years.</summary>
    public static double StarLifespan(double massSolar)
        => massSolar <= 0.0 ? 0.0 : 10.0 * Math.Pow(massSolar, -2.5);

    public static (double Inner, double Outer) HabitableZone(double luminositySolar)
        => (Math.Sqrt(luminositySolar / 1.1), Math.Sqrt(luminositySolar / 0.53));

    public static double SnowLine(double luminositySolar)
        => 2.7 * Math.Sqrt(luminositySolar);

    internal static double ComputeOrbitalPeriodDays(double semiMajorAxisAu, double starMassSolar)
        => 365.25 * Math.Sqrt(Math.Pow(semiMajorAxisAu, 3.0) / starMassSolar);

    internal static double EquilibriumTempK(double luminositySolar, double orbitalAu, double albedo)
    {
        var starTerm = Math.Sqrt(Math.Sqrt(luminositySolar / (orbitalAu * orbitalAu)));
        var albedoTerm = Math.Sqrt(Math.Sqrt(1.0 - albedo));
        return 278.0 * starTerm * albedoTerm;
    }

    internal static double MutualHillAu(
        double a1,
        double mass1Earth,
        double a2,
        double mass2Earth,
        double starMassSolar)
    {
        var meanA = (a1 + a2) * 0.5;
        var massSolar = (mass1Earth + mass2Earth) / EarthMassesPerSolar;
        return meanA * Math.Cbrt(massSolar / (3.0 * starMassSolar));
    }

    internal static bool HillSeparated(
        double a1,
        double mass1Earth,
        double a2,
        double mass2Earth,
        double starMassSolar)
        => Math.Abs(a2 - a1) >= MinHillSeparation * MutualHillAu(a1, mass1Earth, a2, mass2Earth, starMassSolar);

    internal static double GiantRadiusEarthRadii(double massEarth)
        => 2.0 * Math.Sqrt(Math.Sqrt(massEarth));

    internal static double IceGiantRadius(double massEarth)
        => 3.2 * Math.Sqrt(Math.Sqrt(massEarth / 17.0));

    internal static double WorldRadiusFromMass(double massEarth)
        => Math.Pow(Math.Max(0.0, massEarth), 0.27);

    internal static double MoonOrbitalPeriodDays(double giantMassEarth, double moonOrbitEarthRadii)
    {
        var giantMassSolar = giantMassEarth / EarthMassesPerSolar;
        var moonOrbitAu = moonOrbitEarthRadii / EarthRadiiPerAu;
        return ComputeOrbitalPeriodDays(moonOrbitAu, giantMassSolar);
    }

    internal static double GiantHillSphereEarthRadii(
        double giantAu,
        double giantMassEarth,
        double starMassSolar)
    {
        var massRatio = (giantMassEarth / EarthMassesPerSolar) / (3.0 * starMassSolar);
        return giantAu * Math.Cbrt(massRatio) * EarthRadiiPerAu;
    }

    internal static double ComputeRocheLimitEarthRadii(double giantRadiusEarth, double moonRadiusEarth)
    {
        const double giantDensity = 700.0;
        const double moonDensity = 5514.0;
        return 2.44 * giantRadiusEarth * Math.Cbrt(giantDensity / moonDensity);
    }

    internal static double MaxHabitableMoonOrbitEarthRadii(double giantMassEarth)
    {
        var giantMassSolar = giantMassEarth / EarthMassesPerSolar;
        var periodRatio = MaxMoonDayDays / 365.25;
        var au = Math.Cbrt(giantMassSolar * periodRatio * periodRatio);
        return au * EarthRadiiPerAu * 0.995;
    }

    internal static LocalSystem Fallback()
    {
        const double luminosity = 1.0;
        var (inner, outer) = HabitableZone(luminosity);
        var shepherdMass = 318.0;
        var shepherdAu = 5.2;
        return new LocalSystem(
            StarSpectralClass.G,
            StarMassSolar: 1.0,
            StarRadiusSolar: 1.0,
            LuminositySolar: luminosity,
            StarLifespanGyr: 10.0,
            inner,
            outer,
            OrbitalDistanceAu: 1.0,
            OrbitalPeriodDays: 365.25,
            BondAlbedo: 0.30,
            GreenhouseDeltaK: 33.0,
            SnowLineAu: SnowLine(luminosity),
            ParentGiantMassEarth: null,
            MoonOrbitalDistanceEarthRadii: null,
            MoonDayLengthDays: null,
            RocheLimitEarthRadii: null,
            [
                new CompanionPlanet(
                    CompanionRole.ShepherdGiant,
                    shepherdAu,
                    shepherdMass,
                    GiantRadiusEarthRadii(shepherdMass),
                    ComputeOrbitalPeriodDays(shepherdAu, 1.0))
            ],
            [],
            HabitableMoonIndex: null);
    }

    private static bool TrySample(
        ref SplitMix64 rng,
        ObserverWorldKind kind,
        EarthAnalogWorld bulk,
        out LocalSystem system,
        out EarthAnalogWorld world)
    {
        system = Fallback();
        world = bulk;

        var asMoon = kind == ObserverWorldKind.TerrestrialMoon;
        var hosts = asMoon ? MoonHostClasses : PlanetHostClasses;
        var starClass = hosts[rng.NextInt(hosts.Length)];
        var (minMass, maxMass) = MassRange(starClass);
        var starMass = rng.NextRange(minMass, maxMass);
        var luminosity = MassLuminosity(starMass);
        var starRadius = ComputeStarRadiusSolar(starMass);
        var lifespan = StarLifespan(starMass);
        if (lifespan < MinStarLifespanGyr)
        {
            return false;
        }

        var (innerHz, outerHz) = HabitableZone(luminosity);
        var orbitalAu = PickOrbitalDistance(ref rng, innerHz, outerHz);
        var yearDays = ComputeOrbitalPeriodDays(orbitalAu, starMass);
        if (!asMoon && yearDays < MinPlanetYearDays)
        {
            return false;
        }

        var albedo = rng.NextRange(0.25, 0.35);
        var greenhouse = rng.NextRange(28.0, 38.0);
        var (eqTemp, surfTemp, finalAu, finalGreenhouse) = BalanceClimate(
            luminosity,
            orbitalAu,
            albedo,
            greenhouse,
            innerHz,
            outerHz);
        orbitalAu = finalAu;
        yearDays = ComputeOrbitalPeriodDays(orbitalAu, starMass);
        greenhouse = finalGreenhouse;

        if (surfTemp is < EarthAnalog.MinSurfaceTemperatureK or > EarthAnalog.MaxSurfaceTemperatureK)
        {
            return false;
        }

        if (!asMoon && yearDays < MinPlanetYearDays)
        {
            return false;
        }

        double? giantMass = null;
        SystemMoon[] moons = [];
        int? habitableMoonIndex = null;
        double? moonOrbitEarthRadii = null;
        double? moonDay = null;
        double? rocheEarthRadii = null;

        if (asMoon)
        {
            giantMass = rng.NextRange(100.0, 300.0);
            moons = PlaceMoonFamily(ref rng, starMass, orbitalAu, giantMass.Value, bulk);
            var home = moons.First(static moon => moon.Habitable);
            habitableMoonIndex = home.Index;
            moonOrbitEarthRadii = home.OrbitalDistanceEarthRadii;
            moonDay = home.DayLengthDays;
            rocheEarthRadii = ComputeRocheLimitEarthRadii(GiantRadiusEarthRadii(giantMass.Value), bulk.RadiusEarth);
            if (moonOrbitEarthRadii <= rocheEarthRadii
                || moonDay > MaxMoonDayDays
                || moonDay < MinMoonDayDays)
            {
                return false;
            }
        }

        var habitableMass = asMoon ? giantMass ?? bulk.MassEarth : bulk.MassEarth;
        var snowLine = SnowLine(luminosity);
        var companions = PlaceCompanions(
            ref rng,
            starMass,
            snowLine,
            innerHz,
            outerHz,
            orbitalAu,
            habitableMass);

        system = new LocalSystem(
            starClass,
            starMass,
            starRadius,
            luminosity,
            lifespan,
            innerHz,
            outerHz,
            orbitalAu,
            yearDays,
            albedo,
            greenhouse,
            snowLine,
            giantMass,
            moonOrbitEarthRadii,
            moonDay,
            rocheEarthRadii,
            companions,
            moons,
            habitableMoonIndex);
        world = EarthAnalog.WithClimate(bulk, eqTemp, surfTemp);
        return EarthAnalog.IsEarthlike(world);
    }

    private static readonly StarSpectralClass[] PlanetHostClasses =
    [
        StarSpectralClass.K,
        StarSpectralClass.G,
        StarSpectralClass.F
    ];

    private static readonly StarSpectralClass[] MoonHostClasses =
    [
        StarSpectralClass.M,
        StarSpectralClass.K,
        StarSpectralClass.G,
        StarSpectralClass.F
    ];

    private static double PickOrbitalDistance(ref SplitMix64 rng, double innerHz, double outerHz)
    {
        var span = outerHz - innerHz;
        var center = innerHz + (0.35 * span) + (rng.NextUnit() * 0.30 * span);
        return Math.Clamp(center, innerHz * 1.02, outerHz * 0.98);
    }

    private static (double EqTemp, double SurfTemp, double OrbitalAu, double Greenhouse) BalanceClimate(
        double luminosity,
        double orbitalAu,
        double albedo,
        double greenhouse,
        double innerHz,
        double outerHz)
    {
        var au = orbitalAu;
        var gh = greenhouse;

        for (var pass = 0; pass < 16; pass++)
        {
            var eq = EquilibriumTempK(luminosity, au, albedo);
            var surf = eq + gh;
            if (surf is >= EarthAnalog.MinSurfaceTemperatureK and <= EarthAnalog.MaxSurfaceTemperatureK)
            {
                return (eq, surf, au, gh);
            }

            if (surf < EarthAnalog.MinSurfaceTemperatureK)
            {
                gh += 4.0;
                if (gh > 55.0 && au > innerHz * 1.01)
                {
                    au *= 0.96;
                    gh = greenhouse;
                }
            }
            else
            {
                gh -= 4.0;
                if (gh < 10.0 && au < outerHz * 0.99)
                {
                    au *= 1.04;
                    gh = greenhouse;
                }
            }

            au = Math.Clamp(au, innerHz * 1.01, outerHz * 0.99);
            gh = Math.Clamp(gh, 8.0, 60.0);
        }

        var finalEq = EquilibriumTempK(luminosity, au, albedo);
        return (finalEq, finalEq + gh, au, gh);
    }

    private static SystemMoon[] PlaceMoonFamily(
        ref SplitMix64 rng,
        double starMassSolar,
        double giantAu,
        double giantMassEarth,
        EarthAnalogWorld habitable)
    {
        var giantRadius = GiantRadiusEarthRadii(giantMassEarth);
        var roche = ComputeRocheLimitEarthRadii(giantRadius, habitable.RadiusEarth);
        var hill = GiantHillSphereEarthRadii(giantAu, giantMassEarth, starMassSolar);
        var inner = roche * 1.12;
        var dayLimit = MaxHabitableMoonOrbitEarthRadii(giantMassEarth);
        var outer = Math.Min(hill * 0.36, dayLimit);
        if (outer < inner)
        {
            outer = inner;
        }

        var count = 1;
        if (outer > inner * 1.18)
        {
            var span = outer / inner;
            for (var n = 2; n <= 8; n++)
            {
                if (Math.Pow(span, 1.0 / (n - 1)) < 1.18)
                {
                    break;
                }

                count = n;
            }

            count = rng.NextInt(1, count + 1);
        }

        var factor = count == 1 ? 1.0 : Math.Pow(outer / inner, 1.0 / (count - 1));
        var oneDayOrbit = Math.Clamp(
            MoonOrbitForPeriodEarthRadii(giantMassEarth, 1.0),
            inner,
            outer);
        var orbits = new double[count];
        if (count == 1)
        {
            orbits[0] = oneDayOrbit;
        }
        else
        {
            for (var i = 0; i < count; i++)
            {
                orbits[i] = inner * Math.Pow(factor, i);
            }
        }

        var home = 0;
        var bestDayError = double.MaxValue;
        for (var i = 0; i < count; i++)
        {
            var dayError = Math.Abs(MoonOrbitalPeriodDays(giantMassEarth, orbits[i]) - 1.0);
            if (dayError < bestDayError)
            {
                bestDayError = dayError;
                home = i;
            }
        }

        orbits[home] = oneDayOrbit;

        var moons = new SystemMoon[count];
        for (var i = 0; i < count; i++)
        {
            var habitableMoon = i == home;
            var mass = habitableMoon ? habitable.MassEarth : rng.NextRange(0.008, 0.06);
            var radius = habitableMoon ? habitable.RadiusEarth : WorldRadiusFromMass(mass);
            moons[i] = new SystemMoon(
                Index: i + 1,
                OrbitalDistanceEarthRadii: orbits[i],
                MassEarth: mass,
                RadiusEarth: radius,
                DayLengthDays: MoonOrbitalPeriodDays(giantMassEarth, orbits[i]),
                Habitable: habitableMoon);
        }

        return moons;
    }

    internal static double MoonOrbitForPeriodEarthRadii(double giantMassEarth, double periodDays)
    {
        var giantMassSolar = giantMassEarth / EarthMassesPerSolar;
        var periodRatio = periodDays / 365.25;
        var au = Math.Cbrt(giantMassSolar * periodRatio * periodRatio);
        return au * EarthRadiiPerAu;
    }

    private static CompanionPlanet[] PlaceCompanions(
        ref SplitMix64 rng,
        double starMassSolar,
        double snowLineAu,
        double innerHz,
        double outerHz,
        double habitableAu,
        double habitableMassEarth)
    {
        var placed = new List<CompanionPlanet>(3)
        {
            PlaceShepherd(ref rng, starMassSolar, snowLineAu, outerHz, habitableAu, habitableMassEarth)
        };

        if (rng.NextBool(0.55))
        {
            var innerAu = rng.NextRange(innerHz * 0.32, innerHz * 0.82);
            var innerMass = rng.NextRange(0.06, 0.70);
            if (innerAu > 0.03
                && HillSeparated(innerAu, innerMass, habitableAu, habitableMassEarth, starMassSolar))
            {
                placed.Add(new CompanionPlanet(
                    CompanionRole.InnerRocky,
                    innerAu,
                    innerMass,
                    WorldRadiusFromMass(innerMass),
                    ComputeOrbitalPeriodDays(innerAu, starMassSolar)));
            }
        }

        if (rng.NextBool(0.40))
        {
            var shepherd = placed[0];
            var iceAu = shepherd.SemiMajorAxisAu * rng.NextRange(1.65, 2.55);
            var iceMass = rng.NextRange(14.0, 22.0);
            if (HillSeparated(shepherd.SemiMajorAxisAu, shepherd.MassEarth, iceAu, iceMass, starMassSolar))
            {
                placed.Add(new CompanionPlanet(
                    CompanionRole.OuterIceGiant,
                    iceAu,
                    iceMass,
                    IceGiantRadius(iceMass),
                    ComputeOrbitalPeriodDays(iceAu, starMassSolar)));
            }
        }

        placed.Sort(static (a, b) => a.SemiMajorAxisAu.CompareTo(b.SemiMajorAxisAu));
        return [.. placed];
    }

    private static CompanionPlanet PlaceShepherd(
        ref SplitMix64 rng,
        double starMassSolar,
        double snowLineAu,
        double outerHz,
        double habitableAu,
        double habitableMassEarth)
    {
        var minAu = snowLineAu * 1.15;
        minAu = Math.Max(minAu, outerHz * 1.25);
        minAu = Math.Max(minAu, habitableAu * 1.8);

        var maxAu = snowLineAu * 2.4;
        if (maxAu < minAu * 1.15)
        {
            maxAu = minAu * 1.35;
        }

        var au = rng.NextRange(minAu, maxAu);
        var mass = rng.NextRange(120.0, 320.0);
        for (var i = 0; i < 20 && !HillSeparated(habitableAu, habitableMassEarth, au, mass, starMassSolar); i++)
        {
            au *= 1.08;
        }

        return new CompanionPlanet(
            CompanionRole.ShepherdGiant,
            au,
            mass,
            GiantRadiusEarthRadii(mass),
            ComputeOrbitalPeriodDays(au, starMassSolar));
    }

    private IReadOnlyList<LocalSystemCheck> EvaluateChecks()
    {
        var habitableMass = ParentGiantMassEarth ?? 1.0;
        var checks = new List<LocalSystemCheck>
        {
            new(
                "Star lifespan",
                StarLifespanGyr >= MinStarLifespanGyr,
                Invariant($"{StarLifespanGyr:F1} Gyr (need >= {MinStarLifespanGyr:F0} Gyr)")),
            new(
                "Habitable zone",
                OrbitalDistanceAu >= HabitableZoneInnerAu && OrbitalDistanceAu <= HabitableZoneOuterAu,
                Invariant($"{OrbitalDistanceAu:F2} AU (HZ {HabitableZoneInnerAu:F2}-{HabitableZoneOuterAu:F2} AU)"))
        };

        if (ParentGiantMassEarth is null)
        {
            checks.Add(new LocalSystemCheck(
                "Planet year",
                OrbitalPeriodDays >= MinPlanetYearDays,
                Invariant($"{OrbitalPeriodDays:F0} d (need >= {MinPlanetYearDays:F0} d so the world is not tidally locked)")));
        }
        else
        {
            var rocheOk = MoonOrbitalDistanceEarthRadii is { } orbit
                          && RocheLimitEarthRadii is { } roche
                          && orbit > roche;
            checks.Add(new LocalSystemCheck(
                "Roche limit",
                rocheOk,
                rocheOk
                    ? Invariant($"Moon at {MoonOrbitalDistanceEarthRadii:F0} Rearth, limit {RocheLimitEarthRadii:F0} Rearth")
                    : "Moon orbit inside Roche limit"));
            checks.Add(new LocalSystemCheck(
                "Tidal day length",
                MoonDayLengthDays is { } day && day <= MaxMoonDayDays,
                MoonDayLengthDays is { } moonDay
                    ? Invariant($"{moonDay:F1} Earth days (max {MaxMoonDayDays:F0})")
                    : "Unknown"));
        }

        var shepherd = Companions.FirstOrDefault(static body => body.Role == CompanionRole.ShepherdGiant);
        var hasShepherd = shepherd is not null;
        var beyondSnow = hasShepherd && shepherd!.SemiMajorAxisAu > SnowLineAu;
        var hillOk = hasShepherd
                     && HillSeparated(
                         OrbitalDistanceAu,
                         habitableMass,
                         shepherd!.SemiMajorAxisAu,
                         shepherd.MassEarth,
                         StarMassSolar);
        checks.Add(new LocalSystemCheck(
            "Shepherd giant",
            hasShepherd && beyondSnow && hillOk,
            hasShepherd
                ? Invariant(
                    $"{shepherd!.MassEarth:F0} Mearth at {shepherd.SemiMajorAxisAu:F2} AU (snow line {SnowLineAu:F2} AU)")
                : "No outer giant to scatter leftover planetesimals"));

        return checks;
    }

    private static string Invariant(FormattableString value)
        => value.ToString(CultureInfo.InvariantCulture);
}
