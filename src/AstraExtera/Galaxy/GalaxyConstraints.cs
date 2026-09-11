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
        var warnings = new List<string>(2);
        var reconciled = this;

        if (MoonWorldAskedForMoons is string moons)
        {
            warnings.Add($"{moons} Ignoring HomeMoons.");
            reconciled = reconciled with { HomeMoons = PresenceConstraint.Any };
        }

        if (StarClassWithNoHostWorld is string star)
        {
            warnings.Add($"{star} Ignoring StarClass.");
            reconciled = reconciled with { StarClass = StarClassConstraint.Any };
        }

        return (reconciled, warnings);
    }

    /// <summary>
    /// The same contradictions <see cref="Reconcile"/> finds, stated without what it does about
    /// them.
    /// </summary>
    /// <remarks>
    /// A config file and a typed command want opposite things from a contradiction. A config is
    /// read while a save is loading, where the only acceptable outcome is a world, so it widens the
    /// offending setting and warns. Someone typing <c>/astraextera find</c> has asked a question
    /// with no answer, and silently answering a different question would send them hunting through
    /// a search that was never going to converge. So the command refuses and quotes these.
    /// </remarks>
    public IReadOnlyList<string> Contradictions()
        => [.. new[] { MoonWorldAskedForMoons, StarClassWithNoHostWorld }.OfType<string>()];

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

    /// <summary>
    /// This set taken apart into one single-setting set per thing actually asked for, which is how
    /// a search that found nothing can say which part of the request was the impossible one rather
    /// than only that the whole of it failed.
    /// </summary>
    public IReadOnlyList<GalaxyConstraints> Clauses()
    {
        var clauses = new List<GalaxyConstraints>(5);
        if (WorldKind != WorldKindConstraint.Any)
        {
            clauses.Add(new GalaxyConstraints(WorldKind: WorldKind));
        }

        if (StarClass != StarClassConstraint.Any)
        {
            clauses.Add(new GalaxyConstraints(StarClass: StarClass));
        }

        if (GalaxyMorphology != MorphologyConstraint.Any)
        {
            clauses.Add(new GalaxyConstraints(GalaxyMorphology: GalaxyMorphology));
        }

        if (ParentGiantRings != PresenceConstraint.Any)
        {
            clauses.Add(new GalaxyConstraints(ParentGiantRings: ParentGiantRings));
        }

        if (HomeMoons != PresenceConstraint.Any)
        {
            clauses.Add(new GalaxyConstraints(HomeMoons: HomeMoons));
        }

        return clauses;
    }

    /// <summary>The vocabulary, for a usage line.</summary>
    public static string Usage
        => $"Constraints are kind={Allowed<WorldKindConstraint>()}; star={Allowed<StarClassConstraint>()}; "
           + $"galaxy={Allowed<MorphologyConstraint>()}; rings={Allowed<PresenceConstraint>()}; "
           + $"moons={Allowed<PresenceConstraint>()}.";

    /// <summary>
    /// Reads the <c>kind=moon star=K rings=required</c> form an admin types, which is exactly what
    /// <see cref="Describe"/> writes -- so the <c>constraints=</c> tail of
    /// <c>/astraextera galaxy</c> can be pasted straight back in to look for another world like
    /// this one.
    /// </summary>
    public static bool TryParse(string? text, out GalaxyConstraints constraints, out string error)
    {
        constraints = Unconstrained;
        error = string.Empty;
        var tokens = (text ?? string.Empty).Split(
            [' ', '\t', ','],
            StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        foreach (var token in tokens)
        {
            var split = token.IndexOf('=');
            if (split <= 0 || split == token.Length - 1)
            {
                error = $"'{token}' is not a key=value constraint. {Usage}";
                return false;
            }

            var key = token[..split];
            var value = token[(split + 1)..];
            switch (key.ToLowerInvariant())
            {
                case "kind" when TryValue<WorldKindConstraint>(key, value, out var kind, out error):
                    constraints = constraints with { WorldKind = kind };
                    break;
                case "star" when TryValue<StarClassConstraint>(key, value, out var star, out error):
                    constraints = constraints with { StarClass = star };
                    break;
                case "galaxy" when TryValue<MorphologyConstraint>(key, value, out var galaxy, out error):
                    constraints = constraints with { GalaxyMorphology = galaxy };
                    break;
                case "rings" when TryValue<PresenceConstraint>(key, value, out var rings, out error):
                    constraints = constraints with { ParentGiantRings = rings };
                    break;
                case "moons" when TryValue<PresenceConstraint>(key, value, out var moons, out error):
                    constraints = constraints with { HomeMoons = moons };
                    break;
                case "kind" or "star" or "galaxy" or "rings" or "moons":
                    return false;
                default:
                    error = $"'{key}' is not a constraint. {Usage}";
                    return false;
            }
        }

        return true;
    }

    /// <summary>What a setting accepts, written the way it is typed rather than the way it is named.</summary>
    public static string Allowed<T>(string separator = "|") where T : struct, Enum
        => string.Join(
            separator,
            Enum.GetNames<T>().Select(static name =>
                name.Equals("Any", StringComparison.Ordinal) ? AnyValue : name.ToLowerInvariant()));

    /// <summary>Everything that is not a constraint, which is the same word everywhere it is read.</summary>
    public const string AnyValue = "any";

    private static bool TryValue<T>(string setting, string value, out T parsed, out string error)
        where T : struct, Enum
    {
        if (Enum.TryParse(value, ignoreCase: true, out parsed) && Enum.IsDefined(parsed))
        {
            error = string.Empty;
            return true;
        }

        parsed = default;
        error = $"{setting} '{value}' is not one of {Allowed<T>()}.";
        return false;
    }

    /// <summary>Why a moon world cannot also be asked for moons, or null when it was not.</summary>
    private string? MoonWorldAskedForMoons
        => WorldKind == WorldKindConstraint.Moon && HomeMoons == PresenceConstraint.Required
            ? "HomeMoons 'required' asks a moon world for moons of its own; a moon world's family "
              + "belongs to its giant."
            : null;

    /// <summary>Why no world at all can sit under the star that was asked for, or null when one can.</summary>
    private string? StarClassWithNoHostWorld
        => PermittedWorldKinds().Count == 0
            ? $"StarClass '{StarClass}' cannot host a {WorldKind.ToString().ToLowerInvariant()} world."
            : null;

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
