namespace AstraExtera.Galaxy;

/// <summary>What a seed hunt turned up, or why it came back empty-handed.</summary>
/// <param name="Match">The first placement that matched, or null when none did.</param>
/// <param name="Attempts">How many seeds were drawn, matched or not.</param>
/// <param name="Unsatisfied">
/// The parts of the request that nothing drawn satisfied even on their own, written in the
/// <c>kind=moon</c> form they were asked for. Empty when the search succeeded -- and also when it
/// failed with every part individually reachable, which is the difference between a request that
/// cannot be met and one that is merely rare.
/// </param>
public sealed record GalaxySearchResult(
    GalaxyPlacement? Match,
    int Attempts,
    IReadOnlyList<string> Unsatisfied)
{
    public bool Found => Match is not null;
}

/// <summary>
/// Draws seeds until one produces the world an admin asked for, without applying any of them.
/// </summary>
/// <remarks>
/// <para>
/// This searches <see cref="GalaxyPlacement"/>s rather than whole <see cref="GalaxySky"/>s. Every
/// constraint is answerable from the placement, and the placement is where the generator stops
/// before <see cref="StarFieldSampler"/> samples ten thousand stars -- so a sweep of hundreds of
/// seeds costs a fraction of one <c>/astraextera preview</c>. The seed it reports is then previewed
/// or applied, and that is where the sky gets built.
/// </para>
/// <para>
/// Candidates are drawn under the server's own configured constraints, not under the ones being
/// searched for. That is the whole point: a reported seed has to be a seed
/// <c>/astraextera reroll</c> would turn into the world that was previewed, and reroll generates
/// under the server config. Searching under the typed constraints instead would find seeds that
/// produce something else entirely when applied.
/// </para>
/// </remarks>
public static class GalaxySkySearch
{
    /// <summary>
    /// How many seeds a search draws before giving up.
    /// </summary>
    /// <remarks>
    /// A chat command runs on the server thread, so this is a latency budget rather than a
    /// thoroughness one. Placement generation is cheap enough that this stays well inside a tick's
    /// worth of work, and it is deep enough for the rarest single constraint -- an elliptical
    /// galaxy, at <see cref="GalaxyGenerator.EllipticalProbability"/> -- to turn up several times.
    /// A combination rarer than this is better served by widening the request than by waiting.
    /// </remarks>
    public const int DefaultAttempts = 512;

    public static GalaxySearchResult Find(
        GalaxyConstraints wanted,
        GalaxyConstraints? authoring = null,
        IEnumerable<long>? seeds = null,
        int maxAttempts = DefaultAttempts)
    {
        ArgumentNullException.ThrowIfNull(wanted);

        // Each part of the request is tested on its own as well as together, so a search that found
        // nothing can say which part was impossible instead of only that the whole of it failed.
        var clauses = wanted.Clauses();
        var met = new bool[clauses.Count];
        var attempts = 0;

        foreach (var seed in seeds ?? RandomSeeds())
        {
            if (attempts >= maxAttempts)
            {
                break;
            }

            attempts++;
            var placement = GalaxyGenerator.Generate(seed, authoring);
            for (var i = 0; i < clauses.Count; i++)
            {
                met[i] = met[i] || clauses[i].IsSatisfiedBy(placement);
            }

            if (wanted.IsSatisfiedBy(placement))
            {
                return new GalaxySearchResult(placement, attempts, []);
            }
        }

        return new GalaxySearchResult(
            null,
            attempts,
            [.. clauses.Where((_, i) => !met[i]).Select(static clause => clause.Describe())]);
    }

    /// <summary>An endless supply of seeds, which is what an admin hunting for a world wants.</summary>
    public static IEnumerable<long> RandomSeeds()
    {
        while (true)
        {
            yield return Random.Shared.NextInt64();
        }
    }
}
