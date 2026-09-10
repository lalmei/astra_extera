namespace AstraExtera.Galaxy;

/// <summary>Which kind of world a server is asking the generator for.</summary>
public enum WorldKindConstraint
{
    Any,
    Planet,
    Moon
}

/// <summary>Which spectral class the host star has to be.</summary>
public enum StarClassConstraint
{
    Any,
    M,
    K,
    G,
    F
}

/// <summary>Which shape of galaxy the world has to sit in.</summary>
public enum MorphologyConstraint
{
    Any,
    Spiral,
    Elliptical
}

/// <summary>Whether something a world may or may not have is asked for, refused, or left to chance.</summary>
public enum PresenceConstraint
{
    Any,
    Required,
    None
}

/// <summary>
/// What a server owner asks of the generator, expressed as a narrowing of the candidate set rather
/// than as values to write into the result.
/// </summary>
/// <remarks>
/// <para>
/// The pipeline is a rejection sampler at every level: <see cref="LocalSystem.MaxAttempts"/> draws
/// for a system, <see cref="GalaxyGenerator.MaxLocationAttempts"/> for a site. Every one of those
/// draws is checked against the habitable zone, the Roche limit, Hill separation, the star's
/// lifespan and <see cref="LocalSystem.MinPlanetYearDays"/>. A constraint that narrowed the
/// candidate set and let the sampler keep drawing therefore keeps all of that; a config that let an
/// operator write values straight into the placement would let them author a world that cannot
/// exist. So nothing here is a value. Each setting either removes options from a choice the
/// generator was going to make anyway, or rejects a finished draw.
/// </para>
/// <para>
/// Every setting defaults to <c>Any</c>, and an all-<c>Any</c> set is the generator's own behaviour
/// exactly -- the same seed gives the same placement it gave before any of this existed. See
/// <see cref="IsUnconstrained"/>, which is what the generator checks before spending a wider
/// attempt budget.
/// </para>
/// </remarks>
public sealed record GalaxyConstraints(
    WorldKindConstraint WorldKind = WorldKindConstraint.Any,
    StarClassConstraint StarClass = StarClassConstraint.Any,
    MorphologyConstraint GalaxyMorphology = MorphologyConstraint.Any,
    PresenceConstraint ParentGiantRings = PresenceConstraint.Any,
    PresenceConstraint HomeMoons = PresenceConstraint.Any)
{
    /// <summary>Ask for nothing, which is what the generator did before there was a config.</summary>
    public static readonly GalaxyConstraints Unconstrained = new();

    public bool IsUnconstrained
        => WorldKind == WorldKindConstraint.Any
           && StarClass == StarClassConstraint.Any
           && GalaxyMorphology == MorphologyConstraint.Any
           && ParentGiantRings == PresenceConstraint.Any
           && HomeMoons == PresenceConstraint.Any;

    /// <summary>
    /// The world kinds this set still allows, which is not just <see cref="WorldKind"/>: a planet
    /// world is never given an M dwarf (see <see cref="LocalSystem.PlanetHostClasses"/>, whose
    /// closest habitable orbit would tidally lock it), so asking for an M-type star is asking for a
    /// moon world whether or not the operator said so.
    /// </summary>
    public IReadOnlyList<ObserverWorldKind> PermittedWorldKinds()
    {
        var kinds = new List<ObserverWorldKind>(2);
        if (WorldKind != WorldKindConstraint.Moon && AllowsStarClassFor(ObserverWorldKind.TerrestrialPlanet))
        {
            kinds.Add(ObserverWorldKind.TerrestrialPlanet);
        }

        if (WorldKind != WorldKindConstraint.Planet && AllowsStarClassFor(ObserverWorldKind.TerrestrialMoon))
        {
            kinds.Add(ObserverWorldKind.TerrestrialMoon);
        }

        return kinds;
    }

    /// <summary>The kind to generate, given the one the generator's own roll came up with.</summary>
    public ObserverWorldKind ResolveWorldKind(ObserverWorldKind rolled)
    {
        var permitted = PermittedWorldKinds();
        return permitted.Count == 0 || permitted.Contains(rolled) ? rolled : permitted[0];
    }

    /// <summary>The host classes left after this set has had its say, in the sampler's own order.</summary>
    public StarSpectralClass[] Narrow(StarSpectralClass[] hosts)
    {
        ArgumentNullException.ThrowIfNull(hosts);
        if (StarClass == StarClassConstraint.Any)
        {
            return hosts;
        }

        var wanted = ToSpectralClass(StarClass);
        return [.. hosts.Where(host => host == wanted)];
    }

    /// <summary>Whether a finished placement is what was asked for.</summary>
    public bool IsSatisfiedBy(GalaxyPlacement placement)
    {
        ArgumentNullException.ThrowIfNull(placement);

        if (WorldKind != WorldKindConstraint.Any && placement.WorldKind != ToWorldKind(WorldKind))
        {
            return false;
        }

        if (StarClass != StarClassConstraint.Any && placement.System.StarClass != ToSpectralClass(StarClass))
        {
            return false;
        }

        if (GalaxyMorphology != MorphologyConstraint.Any
            && placement.Galaxy.IsElliptical != (GalaxyMorphology == MorphologyConstraint.Elliptical))
        {
            return false;
        }

        if (ParentGiantRings != PresenceConstraint.Any)
        {
            var hasRing = DominantGiant(placement.System)?.Ring is not null;
            if (hasRing != (ParentGiantRings == PresenceConstraint.Required))
            {
                return false;
            }
        }

        if (HomeMoons != PresenceConstraint.Any
            && placement.WorldKind == ObserverWorldKind.TerrestrialPlanet)
        {
            var hasMoons = placement.System.HomeMoons.Length > 0;
            if (hasMoons != (HomeMoons == PresenceConstraint.Required))
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>
    /// The giant whose rings <see cref="ParentGiantRings"/> is about: the one the world orbits, if
    /// it is a moon, and otherwise the shepherd giant past the snow line, which is the giant a
    /// planet world actually sees.
    /// </summary>
    public static GiantAppearance? DominantGiant(LocalSystem system)
    {
        ArgumentNullException.ThrowIfNull(system);
        return system.ParentGiantAppearance
               ?? system.Companions
                   .FirstOrDefault(static companion => companion.Role == CompanionRole.ShepherdGiant)
                   ?.Appearance;
    }

    /// <summary>
    /// Settings that cannot all hold at once, each named with why, together with the set left after
    /// the offending ones are widened back to <c>Any</c>. A contradiction is an operator mistake,
    /// not an emergency: a save still has to load, so this reports rather than throws.
    /// </summary>
    public (GalaxyConstraints Reconciled, IReadOnlyList<string> Warnings) Reconcile()
    {
        var warnings = new List<string>();
        var reconciled = this;

        if (WorldKind == WorldKindConstraint.Moon && HomeMoons == PresenceConstraint.Required)
        {
            warnings.Add(
                "HomeMoons 'required' asks a moon world for moons of its own; a moon world's family "
                + "belongs to its giant. Ignoring HomeMoons.");
            reconciled = reconciled with { HomeMoons = PresenceConstraint.Any };
        }

        if (PermittedWorldKinds().Count == 0)
        {
            warnings.Add(
                $"StarClass '{StarClass}' cannot host a {WorldKind.ToString().ToLowerInvariant()} world. "
                + "Ignoring StarClass.");
            reconciled = reconciled with { StarClass = StarClassConstraint.Any };
        }

        return (reconciled, warnings);
    }

    /// <summary>How this set reads in a log line or in <c>/astraextera galaxy</c>.</summary>
    public string Describe()
    {
        if (IsUnconstrained)
        {
            return "none";
        }

        var parts = new List<string>(5);
        if (WorldKind != WorldKindConstraint.Any)
        {
            parts.Add($"kind={WorldKind.ToString().ToLowerInvariant()}");
        }

        if (StarClass != StarClassConstraint.Any)
        {
            parts.Add($"star={StarClass}");
        }

        if (GalaxyMorphology != MorphologyConstraint.Any)
        {
            parts.Add($"galaxy={GalaxyMorphology.ToString().ToLowerInvariant()}");
        }

        if (ParentGiantRings != PresenceConstraint.Any)
        {
            parts.Add($"rings={ParentGiantRings.ToString().ToLowerInvariant()}");
        }

        if (HomeMoons != PresenceConstraint.Any)
        {
            parts.Add($"moons={HomeMoons.ToString().ToLowerInvariant()}");
        }

        return string.Join(",", parts);
    }

    /// <summary>Null and an all-<c>Any</c> set mean the same thing, and both are the common case.</summary>
    public static GalaxyConstraints OrUnconstrained(GalaxyConstraints? constraints)
        => constraints is null || constraints.IsUnconstrained ? Unconstrained : constraints;

    /// <summary>What to record on a placement: nothing at all when nothing was asked for.</summary>
    public static GalaxyConstraints? OrNull(GalaxyConstraints? constraints)
        => constraints is null || constraints.IsUnconstrained ? null : constraints;

    internal static ObserverWorldKind ToWorldKind(WorldKindConstraint kind)
        => kind == WorldKindConstraint.Moon
            ? ObserverWorldKind.TerrestrialMoon
            : ObserverWorldKind.TerrestrialPlanet;

    internal static StarSpectralClass ToSpectralClass(StarClassConstraint starClass) => starClass switch
    {
        StarClassConstraint.M => StarSpectralClass.M,
        StarClassConstraint.K => StarSpectralClass.K,
        StarClassConstraint.G => StarSpectralClass.G,
        StarClassConstraint.F => StarSpectralClass.F,
        _ => StarSpectralClass.G
    };

    private bool AllowsStarClassFor(ObserverWorldKind kind)
        => StarClass == StarClassConstraint.Any
           || LocalSystem.HostClassesFor(kind).Contains(ToSpectralClass(StarClass));
}

/// <summary>
/// What happened when a placement was authored under a constraint set: what was asked for, what was
/// actually applied after contradictions were widened away, and whether the result met it.
/// </summary>
/// <remarks>
/// The generator is pure and has no logger, and a server owner who has misconfigured their world
/// needs to be told rather than left reading a sky that quietly ignored them. So the generator
/// reports and the caller -- <c>GalaxyServerSync</c> on a server, a test elsewhere -- decides what
/// to do about it.
/// </remarks>
public sealed record GalaxyConstraintOutcome(
    GalaxyConstraints Requested,
    GalaxyConstraints Effective,
    bool Satisfied,
    int Attempts,
    IReadOnlyList<string> Warnings)
{
    public bool IsClean => Satisfied && Warnings.Count == 0;

    /// <summary>Every line a server should log about this authoring, contradictions first.</summary>
    public IEnumerable<string> Messages()
    {
        foreach (var warning in Warnings)
        {
            yield return $"AstraExtera config: {warning}";
        }

        if (!Satisfied)
        {
            yield return
                $"AstraExtera could not generate a world matching constraints '{Requested.Describe()}' "
                + $"in {Attempts} attempts; this sky was generated without them. "
                + "Widen or remove the constraint in ModConfig/astraextera.json.";
        }
    }
}
