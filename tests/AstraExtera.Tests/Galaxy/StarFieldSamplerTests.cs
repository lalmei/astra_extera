using AstraExtera.Galaxy;
using Xunit;

namespace AstraExtera.Tests.Galaxy;

public sealed class StarFieldSamplerTests
{
    [Fact]
    public void The_Same_Seed_Draws_The_Same_Sky()
    {
        var placement = GalaxyGenerator.Generate(42);

        var first = StarFieldSampler.Sample(placement);
        var second = StarFieldSampler.Sample(placement);

        Assert.Equal(first.Stars.Count, second.Stars.Count);
        Assert.Equal(first.Stars[0], second.Stars[0]);
    }

    [Fact]
    public void No_Drawn_Star_Is_Fainter_Than_The_Eye_Limit()
    {
        var options = new StarFieldOptions { LimitingMagnitude = 6.5 };
        var field = StarFieldSampler.Sample(GalaxyGenerator.Generate(42), options);

        Assert.NotEmpty(field.Stars);
        Assert.All(field.Stars, star => Assert.True(star.ApparentMagnitude <= 6.5));
    }

    /// <summary>
    /// The whole point of sampling rather than fixing a catalog: move the observer inward, where
    /// the disk is denser, and more stars clear the eye's limit on their own.
    /// </summary>
    [Fact]
    public void A_Denser_Location_Yields_More_Visible_Stars()
    {
        var placement = GalaxyGenerator.Generate(42);
        Assert.False(placement.Galaxy.IsElliptical);

        var inner = CountAtRadius(placement, placement.Galaxy.InnerHabitableRadiusKpc + 0.2);
        var outer = CountAtRadius(placement, placement.Galaxy.OuterHabitableRadiusKpc - 0.2);

        Assert.True(
            inner > outer,
            $"inner disk saw {inner:N0} stars but the outer disk saw {outer:N0}");
    }

    [Fact]
    public void A_Darker_Eye_Limit_Reveals_More_Stars()
    {
        var placement = GalaxyGenerator.Generate(42);

        var nakedEye = StarFieldSampler.Sample(placement, new StarFieldOptions { LimitingMagnitude = 6.5 });
        var darkAdapted = StarFieldSampler.Sample(placement, new StarFieldOptions { LimitingMagnitude = 8.0 });

        Assert.True(darkAdapted.ExpectedVisibleCount > nakedEye.ExpectedVisibleCount);
    }

    /// <summary>
    /// Calibration anchor: Earth sees about 9100 stars brighter than magnitude 6.5. Averaging over
    /// azimuth removes the spiral-arm phase, which on its own swings the count by a factor of two.
    /// </summary>
    [Fact]
    public void A_Solar_Analog_Vantage_Sees_About_As_Many_Stars_As_Earth()
    {
        var placement = GalaxyGenerator.Generate(42);
        var samples = new List<double>();
        for (var i = 0; i < 8; i++)
        {
            var moved = placement with
            {
                Location = placement.Location with
                {
                    GalactocentricRadiusKpc = MetallicityModel.SolarNeighborhoodRadiusKpc,
                    AzimuthRad = i * Math.PI / 4.0,
                    HeightPc = 20.0
                }
            };
            samples.Add(StarFieldSampler.Sample(moved).ExpectedVisibleCount);
        }

        var mean = samples.Average();
        Assert.InRange(mean, 5000.0, 16000.0);
    }

    [Fact]
    public void The_Budget_Keeps_The_Brightest_Stars()
    {
        var placement = GalaxyGenerator.Generate(42);
        var field = StarFieldSampler.Sample(placement, new StarFieldOptions { ResolvedStarBudget = 50 });

        Assert.True(field.Truncated);
        Assert.Equal(50, field.Stars.Count);
        Assert.True(field.UnresolvedCount > 0.0);
        for (var i = 1; i < field.Stars.Count; i++)
        {
            Assert.True(field.Stars[i - 1].ApparentMagnitude <= field.Stars[i].ApparentMagnitude);
        }
    }

    /// <summary>
    /// Where the sky is crowded the budget runs out before the eye does, so the field is a
    /// brightest-first slice rather than everything the observer could technically see.
    /// </summary>
    [Fact]
    public void A_Capped_Field_Reports_The_Brighter_Limit_It_Actually_Reached()
    {
        var placement = GalaxyGenerator.Generate(1234);
        var field = StarFieldSampler.Sample(placement);

        Assert.True(field.Truncated);
        Assert.True(field.EffectiveLimitingMagnitude < field.LimitingMagnitude);
        Assert.Equal(field.Stars[^1].ApparentMagnitude, field.EffectiveLimitingMagnitude);
    }

    [Fact]
    public void A_Sparse_Sky_Reaches_The_Requested_Limit()
    {
        var placement = GalaxyGenerator.Generate(42);
        var field = StarFieldSampler.Sample(placement, new StarFieldOptions { ResolvedStarBudget = int.MaxValue });

        Assert.False(field.Truncated);
        Assert.True(field.EffectiveLimitingMagnitude <= field.LimitingMagnitude);
    }

    [Fact]
    public void Dust_Reddens_The_Budget_By_Extinguishing_Distant_Stars()
    {
        var placement = GalaxyGenerator.Generate(42);
        var field = StarFieldSampler.Sample(placement);

        Assert.Contains(field.Stars, star => star.ExtinctionMagnitudes > 0.05);
        Assert.All(field.Stars, star => Assert.True(star.ExtinctionMagnitudes >= 0.0));
    }

    /// <summary>
    /// Naked-eye stars are mostly local, so the concentration is mild rather than a tight band --
    /// but a midplane observer should still see more of them toward the disk than toward the poles.
    /// </summary>
    [Fact]
    public void Stars_Concentrate_Toward_The_Galactic_Plane()
    {
        var placement = GalaxyGenerator.Generate(42) with
        {
            Location = GalaxyGenerator.Generate(42).Location with { HeightPc = 0.0 }
        };
        var field = StarFieldSampler.Sample(placement, new StarFieldOptions { ResolvedStarBudget = int.MaxValue });

        var nearPlane = field.Stars.Count(star => Math.Abs(star.GalacticLatitudeRad) < Math.PI / 12.0);
        var nearPoles = field.Stars.Count(star => Math.Abs(star.GalacticLatitudeRad) > 5.0 * Math.PI / 12.0);

        Assert.True(nearPlane > nearPoles, $"{nearPlane} near the plane vs {nearPoles} near the poles");
    }

    [Fact]
    public void An_Elliptical_World_Still_Gets_A_Star_Field()
    {
        GalaxyPlacement? elliptical = null;
        for (long seed = 1; seed <= 800 && elliptical is null; seed++)
        {
            var candidate = GalaxyGenerator.Generate(seed);
            if (candidate.Galaxy.IsElliptical)
            {
                elliptical = candidate;
            }
        }

        Assert.NotNull(elliptical);
        var field = StarFieldSampler.Sample(elliptical);

        Assert.NotEmpty(field.Stars);
        Assert.All(field.Stars, star => Assert.True(star.ApparentMagnitude <= field.LimitingMagnitude));
    }

    private static double CountAtRadius(GalaxyPlacement placement, double radiusKpc)
    {
        var moved = placement with
        {
            Location = placement.Location with
            {
                GalactocentricRadiusKpc = radiusKpc,
                HeightPc = 0.0
            }
        };

        return StarFieldSampler.Sample(moved).ExpectedVisibleCount;
    }
}
