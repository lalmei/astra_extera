using AstraExtera.Galaxy;
using Xunit;

namespace AstraExtera.Tests.Galaxy;

public sealed class GalaxySkySearchTests
{
    // A fixed ladder of seeds, so what this generator happens to produce is pinned rather than
    // redrawn: the counts these tests lean on are properties of these seeds, not of luck.
    private static readonly long[] Seeds = [.. Enumerable.Range(1, 96).Select(static i => i * 7919L)];

    [Fact]
    public void A_Reported_Seed_Is_One_That_Regenerates_The_World_That_Was_Reported()
    {
        var wanted = new GalaxyConstraints(
            WorldKind: WorldKindConstraint.Moon,
            ParentGiantRings: PresenceConstraint.Required);

        var result = GalaxySkySearch.Find(wanted, seeds: Seeds, maxAttempts: Seeds.Length);

        Assert.True(result.Found);
        Assert.Empty(result.Unsatisfied);
        Assert.True(result.Attempts <= Seeds.Length);
        Assert.True(wanted.IsSatisfiedBy(result.Match!));

        // The whole promise of a reported seed: reroll regenerates it, and reroll only has a seed.
        Assert.Equal(GalaxyGenerator.Generate(result.Match!.WorldSeed), result.Match);
    }

    [Fact]
    public void Candidates_Are_Drawn_The_Way_The_Server_Will_Author_Them()
    {
        var authoring = new GalaxyConstraints(StarClass: StarClassConstraint.F);
        var wanted = new GalaxyConstraints(ParentGiantRings: PresenceConstraint.Required);

        var result = GalaxySkySearch.Find(wanted, authoring, Seeds, Seeds.Length);

        Assert.True(result.Found);

        // Drawn under the server's constraints and filtered by the typed ones, so the seed survives
        // a reroll on this server -- and not under the typed ones, which would report a seed that
        // becomes a different world the moment it is applied.
        Assert.Equal(StarSpectralClass.F, result.Match!.System.StarClass);
        Assert.Equal(GalaxyGenerator.Generate(result.Match.WorldSeed, authoring), result.Match);
        Assert.NotEqual(GalaxyGenerator.Generate(result.Match.WorldSeed), result.Match);
    }

    [Fact]
    public void A_Constraint_Nothing_Can_Satisfy_Is_Named_Rather_Than_Only_Counted()
    {
        // Nothing contradictory about wanting a moon; this server has just been configured never to
        // make one, and the search obeys the server.
        var authoring = new GalaxyConstraints(WorldKind: WorldKindConstraint.Planet);
        var wanted = new GalaxyConstraints(
            WorldKind: WorldKindConstraint.Moon,
            ParentGiantRings: PresenceConstraint.Required);

        var result = GalaxySkySearch.Find(wanted, authoring, Seeds, Seeds.Length);

        Assert.False(result.Found);
        Assert.Null(result.Match);
        Assert.Equal(Seeds.Length, result.Attempts);

        // The rings were reachable all along, so naming them too would send an admin to widen the
        // wrong setting.
        Assert.Equal(["kind=moon"], result.Unsatisfied);
    }

    [Fact]
    public void A_Rare_Combination_Is_Not_Reported_As_An_Impossible_One()
    {
        // Over these seeds an elliptical galaxy turns up twice and a moon world thirty-two times,
        // but never the same draw. Nothing here is unsatisfiable; the pair is just scarce.
        var wanted = new GalaxyConstraints(
            WorldKind: WorldKindConstraint.Moon,
            GalaxyMorphology: MorphologyConstraint.Elliptical);

        var result = GalaxySkySearch.Find(wanted, seeds: Seeds, maxAttempts: Seeds.Length);

        Assert.False(result.Found);
        Assert.Empty(result.Unsatisfied);
        foreach (var clause in wanted.Clauses())
        {
            Assert.Contains(Seeds, seed => clause.IsSatisfiedBy(GalaxyGenerator.Generate(seed)));
        }
    }

    [Fact]
    public void A_Hopeless_Search_Stops_At_Its_Budget_Rather_Than_At_Its_Seeds()
    {
        var authoring = new GalaxyConstraints(WorldKind: WorldKindConstraint.Planet);
        var wanted = new GalaxyConstraints(WorldKind: WorldKindConstraint.Moon);

        var result = GalaxySkySearch.Find(wanted, authoring, GalaxySkySearch.RandomSeeds(), maxAttempts: 5);

        Assert.False(result.Found);
        Assert.Equal(5, result.Attempts);
    }

    [Fact]
    public void The_Default_Budget_Is_Deep_Enough_For_The_Rarest_Single_Constraint()
    {
        // An elliptical galaxy is the least likely thing that can be asked for on its own. A budget
        // that could not reach one would make the setting unsearchable.
        var expected = GalaxySkySearch.DefaultAttempts * GalaxyGenerator.EllipticalProbability;
        Assert.True(expected >= 5.0, $"Expected at least five ellipticals per search, got {expected}.");

        var result = GalaxySkySearch.Find(
            new GalaxyConstraints(GalaxyMorphology: MorphologyConstraint.Elliptical),
            seeds: Seeds,
            maxAttempts: Seeds.Length);

        Assert.True(result.Found);
        Assert.True(result.Match!.Galaxy.IsElliptical);
    }
}
