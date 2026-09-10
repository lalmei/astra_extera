using AstraExtera.Config;
using AstraExtera.Galaxy;
using Xunit;

namespace AstraExtera.Tests.Galaxy;

public sealed class GalaxyConstraintsTests
{
    private static readonly long[] Seeds = [.. Enumerable.Range(1, 120).Select(static i => i * 7919L)];

    [Fact]
    public void Asking_For_Nothing_Is_The_Generator_Untouched()
    {
        foreach (var seed in Seeds)
        {
            Assert.Equal(GalaxyGenerator.Generate(seed), GalaxyGenerator.Generate(seed, GalaxyConstraints.Unconstrained));
            Assert.Equal(GalaxyGenerator.Generate(seed), GalaxyGenerator.Generate(seed, new AstraExteraConfig().GetGalaxyConstraints()));
        }
    }

    [Fact]
    public void An_Unconstrained_Placement_Records_No_Constraints_And_Stores_No_Field()
    {
        var placement = GalaxyGenerator.Generate(42);

        Assert.Null(placement.AuthoredUnder);
        Assert.DoesNotContain("authoredUnder", System.Text.Encoding.UTF8.GetString(GalaxyPlacementCodec.ToUtf8(placement)));
        Assert.EndsWith("constraints=none.", GalaxyPlacementCodec.Describe(placement));
    }

    [Theory]
    [InlineData(WorldKindConstraint.Planet, ObserverWorldKind.TerrestrialPlanet)]
    [InlineData(WorldKindConstraint.Moon, ObserverWorldKind.TerrestrialMoon)]
    public void World_Kind_Is_Honoured_On_Every_Seed(WorldKindConstraint asked, ObserverWorldKind expected)
    {
        var constraints = new GalaxyConstraints(WorldKind: asked);
        foreach (var seed in Seeds)
        {
            var placement = GalaxyGenerator.Generate(seed, constraints, out var outcome);

            Assert.True(outcome.Satisfied, $"seed {seed}");
            Assert.Equal(expected, placement.WorldKind);
        }
    }

    [Theory]
    [InlineData(StarClassConstraint.M, StarSpectralClass.M)]
    [InlineData(StarClassConstraint.K, StarSpectralClass.K)]
    [InlineData(StarClassConstraint.G, StarSpectralClass.G)]
    [InlineData(StarClassConstraint.F, StarSpectralClass.F)]
    public void Star_Class_Is_Honoured_On_Every_Seed(StarClassConstraint asked, StarSpectralClass expected)
    {
        var constraints = new GalaxyConstraints(StarClass: asked);
        foreach (var seed in Seeds)
        {
            var placement = GalaxyGenerator.Generate(seed, constraints, out var outcome);

            Assert.True(outcome.Satisfied, $"seed {seed}");
            Assert.Equal(expected, placement.System.StarClass);
        }
    }

    /// <summary>
    /// An M dwarf's habitable zone is close enough in to lock a planet to its star, so
    /// <see cref="LocalSystem.PlanetHostClasses"/> leaves M out. Asking for one is therefore asking
    /// for a moon world, whether or not the operator realised it.
    /// </summary>
    [Fact]
    public void An_M_Dwarf_Implies_A_Moon_World_Without_Being_Asked()
    {
        var constraints = new GalaxyConstraints(StarClass: StarClassConstraint.M);

        Assert.Equal([ObserverWorldKind.TerrestrialMoon], constraints.PermittedWorldKinds());
        foreach (var seed in Seeds)
        {
            Assert.Equal(ObserverWorldKind.TerrestrialMoon, GalaxyGenerator.Generate(seed, constraints).WorldKind);
        }
    }

    [Theory]
    [InlineData(MorphologyConstraint.Elliptical, true)]
    [InlineData(MorphologyConstraint.Spiral, false)]
    public void Galaxy_Morphology_Is_Honoured_On_Every_Seed(MorphologyConstraint asked, bool elliptical)
    {
        var constraints = new GalaxyConstraints(GalaxyMorphology: asked);
        foreach (var seed in Seeds)
        {
            var placement = GalaxyGenerator.Generate(seed, constraints, out var outcome);

            Assert.True(outcome.Satisfied, $"seed {seed}");
            Assert.Equal(elliptical, placement.Galaxy.IsElliptical);
        }
    }

    [Theory]
    [InlineData(PresenceConstraint.Required, true)]
    [InlineData(PresenceConstraint.None, false)]
    public void The_Dominant_Giants_Rings_Are_Honoured_For_Planets_And_Moons_Alike(
        PresenceConstraint asked,
        bool ringed)
    {
        foreach (var kind in new[] { WorldKindConstraint.Planet, WorldKindConstraint.Moon })
        {
            var constraints = new GalaxyConstraints(WorldKind: kind, ParentGiantRings: asked);
            foreach (var seed in Seeds)
            {
                var placement = GalaxyGenerator.Generate(seed, constraints, out var outcome);

                Assert.True(outcome.Satisfied, $"{kind} seed {seed}");
                var giant = GalaxyConstraints.DominantGiant(placement.System);
                Assert.NotNull(giant);
                Assert.Equal(ringed, giant.Ring is not null);
            }
        }
    }

    [Theory]
    [InlineData(PresenceConstraint.Required, true)]
    [InlineData(PresenceConstraint.None, false)]
    public void A_Planet_Worlds_Own_Moons_Are_Honoured_On_Every_Seed(PresenceConstraint asked, bool moons)
    {
        var constraints = new GalaxyConstraints(WorldKind: WorldKindConstraint.Planet, HomeMoons: asked);
        foreach (var seed in Seeds)
        {
            var placement = GalaxyGenerator.Generate(seed, constraints, out var outcome);

            Assert.True(outcome.Satisfied, $"seed {seed}");
            Assert.Equal(moons, placement.System.HomeMoons.Length > 0);
        }
    }

    [Fact]
    public void Every_Constraint_Moves_The_Distribution_It_Names()
    {
        var baseline = Sweep(GalaxyConstraints.Unconstrained);

        Assert.InRange(baseline.Moon, 0.1, 0.5);
        Assert.Equal(1.0, Sweep(new(WorldKind: WorldKindConstraint.Moon)).Moon);
        Assert.Equal(0.0, Sweep(new(WorldKind: WorldKindConstraint.Planet)).Moon);

        Assert.InRange(baseline.Elliptical, 0.0, 0.15);
        Assert.Equal(1.0, Sweep(new(GalaxyMorphology: MorphologyConstraint.Elliptical)).Elliptical);
        Assert.Equal(0.0, Sweep(new(GalaxyMorphology: MorphologyConstraint.Spiral)).Elliptical);

        Assert.InRange(baseline.Ringed, 0.4, 0.95);
        Assert.Equal(1.0, Sweep(new(ParentGiantRings: PresenceConstraint.Required)).Ringed);
        Assert.Equal(0.0, Sweep(new(ParentGiantRings: PresenceConstraint.None)).Ringed);

        var planets = Sweep(new(WorldKind: WorldKindConstraint.Planet));
        Assert.InRange(planets.WithHomeMoons, 0.4, 0.95);
        Assert.Equal(1.0, Sweep(new(WorldKind: WorldKindConstraint.Planet, HomeMoons: PresenceConstraint.Required)).WithHomeMoons);
        Assert.Equal(0.0, Sweep(new(WorldKind: WorldKindConstraint.Planet, HomeMoons: PresenceConstraint.None)).WithHomeMoons);
    }

    /// <summary>
    /// Narrowing the candidate set must not cost draws the sampler does not have: every constraint
    /// is applied by forcing a choice the generator was going to make anyway, so a satisfiable
    /// request lands on the first placement rather than hunting for one.
    /// </summary>
    [Fact]
    public void A_Satisfiable_Request_Costs_One_Placement()
    {
        var constraints = new GalaxyConstraints(
            WorldKind: WorldKindConstraint.Moon,
            StarClass: StarClassConstraint.M,
            GalaxyMorphology: MorphologyConstraint.Elliptical,
            ParentGiantRings: PresenceConstraint.Required);

        foreach (var seed in Seeds)
        {
            GalaxyGenerator.Generate(seed, constraints, out var outcome);

            Assert.True(outcome.IsClean, $"seed {seed}");
            Assert.Equal(1, outcome.Attempts);
        }
    }

    [Fact]
    public void A_Constrained_World_Is_Still_A_Habitable_One()
    {
        var constraints = new GalaxyConstraints(
            StarClass: StarClassConstraint.M,
            ParentGiantRings: PresenceConstraint.Required);

        foreach (var seed in Seeds)
        {
            var placement = GalaxyGenerator.Generate(seed, constraints);

            Assert.True(GalaxyGenerator.IsHabitable(placement.Galaxy, placement.Location), $"seed {seed}");
            Assert.True(EarthAnalog.IsEarthlike(placement.World), $"seed {seed}");
            Assert.True(
                placement.System.IsHabitable,
                $"seed {seed}: {string.Join("; ", placement.System.Checks.Where(static c => !c.Passed).Select(static c => c.Detail))}");
        }
    }

    [Fact]
    public void Asking_A_Moon_World_For_Moons_Of_Its_Own_Is_Reported_And_Ignored()
    {
        var contradiction = new GalaxyConstraints(
            WorldKind: WorldKindConstraint.Moon,
            HomeMoons: PresenceConstraint.Required);

        var (reconciled, warnings) = contradiction.Reconcile();

        Assert.Equal(PresenceConstraint.Any, reconciled.HomeMoons);
        Assert.Equal(WorldKindConstraint.Moon, reconciled.WorldKind);
        Assert.Contains("HomeMoons", Assert.Single(warnings));

        var placement = GalaxyGenerator.Generate(42, contradiction, out var outcome);

        Assert.Equal(ObserverWorldKind.TerrestrialMoon, placement.WorldKind);
        Assert.True(outcome.Satisfied);
        Assert.False(outcome.IsClean);
        Assert.Contains("HomeMoons", string.Join(" ", outcome.Messages()));
    }

    [Fact]
    public void Asking_A_Planet_World_For_An_M_Dwarf_Is_Reported_And_Ignored()
    {
        var contradiction = new GalaxyConstraints(
            WorldKind: WorldKindConstraint.Planet,
            StarClass: StarClassConstraint.M);

        Assert.Empty(contradiction.PermittedWorldKinds());

        var (reconciled, warnings) = contradiction.Reconcile();

        Assert.Equal(StarClassConstraint.Any, reconciled.StarClass);
        Assert.Contains("StarClass", Assert.Single(warnings));

        var placement = GalaxyGenerator.Generate(42, contradiction, out var outcome);

        Assert.Equal(ObserverWorldKind.TerrestrialPlanet, placement.WorldKind);
        Assert.NotEqual(StarSpectralClass.M, placement.System.StarClass);
        Assert.Contains("StarClass", string.Join(" ", outcome.Messages()));
    }

    /// <summary>
    /// A save has to load whatever an operator wrote in the config. Every combination the config can
    /// express -- contradictions included -- produces a habitable world, and the contradictory ones
    /// say so instead of throwing or spinning.
    /// </summary>
    [Fact]
    public void Every_Combination_The_Config_Can_Express_Produces_A_World()
    {
        long[] seeds = [1, 42, -1234, long.MaxValue];
        foreach (var worldKind in Enum.GetValues<WorldKindConstraint>())
        foreach (var starClass in Enum.GetValues<StarClassConstraint>())
        foreach (var morphology in Enum.GetValues<MorphologyConstraint>())
        foreach (var rings in Enum.GetValues<PresenceConstraint>())
        foreach (var homeMoons in Enum.GetValues<PresenceConstraint>())
        {
            var constraints = new GalaxyConstraints(worldKind, starClass, morphology, rings, homeMoons);
            foreach (var seed in seeds)
            {
                var placement = GalaxyGenerator.Generate(seed, constraints, out var outcome);
                var what = $"{constraints.Describe()} seed {seed}";

                // Every set is reachable, because Reconcile widens away the ones that are not.
                Assert.True(outcome.Satisfied, what);
                Assert.True(EarthAnalog.IsEarthlike(placement.World), what);
                Assert.True(placement.System.IsHabitable, what);
                Assert.Equal(outcome.Warnings.Count == 0, outcome.IsClean);
            }
        }
    }

    /// <summary>
    /// The backstop for a set that somehow never converges. Reconcile means no config reaches it,
    /// but a sky that cannot be generated must still be reported rather than silently accepted.
    /// </summary>
    [Fact]
    public void A_Request_That_Never_Converged_Says_Which_Constraints_It_Could_Not_Meet()
    {
        var asked = new GalaxyConstraints(
            WorldKind: WorldKindConstraint.Moon,
            StarClass: StarClassConstraint.M);
        var outcome = new GalaxyConstraintOutcome(
            asked,
            GalaxyConstraints.Unconstrained,
            Satisfied: false,
            GalaxyGenerator.MaxConstrainedAttempts,
            []);

        var message = Assert.Single(outcome.Messages());

        Assert.False(outcome.IsClean);
        Assert.Contains("kind=moon,star=M", message);
        Assert.Contains($"in {GalaxyGenerator.MaxConstrainedAttempts} attempts", message);
        Assert.Contains("astraextera.json", message);
    }

    [Fact]
    public void The_Constraints_A_Sky_Was_Authored_Under_Are_On_The_Placement_And_In_The_Readout()
    {
        var constraints = new GalaxyConstraints(
            WorldKind: WorldKindConstraint.Moon,
            StarClass: StarClassConstraint.K,
            ParentGiantRings: PresenceConstraint.Required);

        var placement = GalaxyGenerator.Generate(42, constraints);

        Assert.Equal(constraints, placement.AuthoredUnder);
        Assert.Equal("kind=moon,star=K,rings=required", constraints.Describe());
        Assert.EndsWith("constraints=kind=moon,star=K,rings=required.", GalaxyPlacementCodec.Describe(placement));
        Assert.Equal(placement, GalaxyPlacementCodec.FromUtf8(GalaxyPlacementCodec.ToUtf8(placement)));
    }

    /// <summary>
    /// A placement stored before constraints existed has no such field, and must still load as the
    /// unconstrained sky it is rather than being regenerated out from under the save.
    /// </summary>
    [Fact]
    public void A_Placement_Saved_Before_Constraints_Existed_Still_Loads()
    {
        var placement = GalaxyGenerator.Generate(42);
        var json = System.Text.Encoding.UTF8.GetString(GalaxyPlacementCodec.ToUtf8(placement));

        var reloaded = GalaxyPlacementCodec.FromUtf8(System.Text.Encoding.UTF8.GetBytes(json));

        Assert.Equal(placement, reloaded);
        Assert.Null(reloaded.AuthoredUnder);
    }

    [Fact]
    public void A_Stored_Sky_Is_Never_Reauthored_To_Match_A_Changed_Config()
    {
        var stored = GalaxyGenerator.Generate(42);
        var constraints = new GalaxyConstraints(WorldKind: WorldKindConstraint.Moon);

        var resolution = GalaxySkyStore.Resolve(
            stored,
            GalaxySky.Author(stored).StarField,
            99,
            GalaxySky.Author(stored).LocalSky,
            constraints,
            out var outcome);

        Assert.False(resolution.PlacementDirty);
        Assert.Equal(stored, resolution.Sky.Placement);
        Assert.Null(outcome);
    }

    [Fact]
    public void A_Fresh_Save_Is_Authored_Under_The_Servers_Constraints()
    {
        var constraints = new GalaxyConstraints(WorldKind: WorldKindConstraint.Moon);

        var resolution = GalaxySkyStore.Resolve(null, null, 42, null, constraints, out var outcome);

        Assert.True(resolution.PlacementDirty);
        Assert.Equal(ObserverWorldKind.TerrestrialMoon, resolution.Sky.Placement.WorldKind);
        Assert.Equal(constraints, resolution.Sky.Placement.AuthoredUnder);
        Assert.True(outcome!.Satisfied);
    }

    [Theory]
    [InlineData("any", WorldKindConstraint.Any)]
    [InlineData("ANY", WorldKindConstraint.Any)]
    [InlineData("", WorldKindConstraint.Any)]
    [InlineData(null, WorldKindConstraint.Any)]
    [InlineData("moon", WorldKindConstraint.Moon)]
    [InlineData("Moon", WorldKindConstraint.Moon)]
    [InlineData("MOON", WorldKindConstraint.Moon)]
    [InlineData(" moon ", WorldKindConstraint.Moon)]
    public void The_Config_Reads_Its_Settings_Case_Insensitively(string? spelling, WorldKindConstraint expected)
    {
        var config = new AstraExteraConfig { WorldKind = spelling! };

        Assert.Equal(expected, config.GetGalaxyConstraints(out var rejected).WorldKind);
        Assert.Empty(rejected);
    }

    [Fact]
    public void A_Misspelt_Setting_Is_Named_And_Widened_Rather_Than_Guessed_At()
    {
        var config = new AstraExteraConfig { StarClass = "K-type", HomeMoons = "yes" };

        var constraints = config.GetGalaxyConstraints(out var rejected);

        Assert.Equal(StarClassConstraint.Any, constraints.StarClass);
        Assert.Equal(PresenceConstraint.Any, constraints.HomeMoons);
        Assert.Equal(2, rejected.Count);
        Assert.Contains(rejected, complaint => complaint.Contains("StarClass 'K-type'") && complaint.Contains("any, m, k, g, f"));
        Assert.Contains(rejected, complaint => complaint.Contains("HomeMoons 'yes'") && complaint.Contains("any, required, none"));
    }

    [Fact]
    public void A_Default_Config_Asks_For_Nothing()
    {
        var config = new AstraExteraConfig();

        Assert.True(config.GetGalaxyConstraints().IsUnconstrained);
        Assert.Equal("none", config.GetGalaxyConstraints().Describe());
    }

    private static (double Moon, double Elliptical, double Ringed, double WithHomeMoons) Sweep(
        GalaxyConstraints constraints)
    {
        double moon = 0, elliptical = 0, ringed = 0, homeMoons = 0;
        foreach (var seed in Seeds)
        {
            var placement = GalaxyGenerator.Generate(seed, constraints);
            if (placement.WorldKind == ObserverWorldKind.TerrestrialMoon)
            {
                moon++;
            }

            if (placement.Galaxy.IsElliptical)
            {
                elliptical++;
            }

            if (GalaxyConstraints.DominantGiant(placement.System)?.Ring is not null)
            {
                ringed++;
            }

            if (placement.System.HomeMoons.Length > 0)
            {
                homeMoons++;
            }
        }

        return (moon / Seeds.Length, elliptical / Seeds.Length, ringed / Seeds.Length, homeMoons / Seeds.Length);
    }
}
