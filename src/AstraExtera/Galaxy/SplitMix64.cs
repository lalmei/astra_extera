namespace AstraExtera.Galaxy;

/// <summary>
/// SplitMix64, used so galaxy draws stay stable across .NET Random algorithm changes.
/// </summary>
public struct SplitMix64
{
    private ulong state;

    public SplitMix64(long seed)
    {
        state = (ulong)seed;
    }

    public ulong NextUInt64()
    {
        unchecked
        {
            state += 0x9E3779B97F4A7C15UL;
            var z = state;
            z = (z ^ (z >> 30)) * 0xBF58476D1CE4E5B9UL;
            z = (z ^ (z >> 27)) * 0x94D049BB133111EBUL;
            return z ^ (z >> 31);
        }
    }

    public double NextUnit()
        => (NextUInt64() >> 11) * (1.0 / (1UL << 53));

    public double NextRange(double minInclusive, double maxExclusive)
        => minInclusive + (maxExclusive - minInclusive) * NextUnit();

    public bool NextBool(double probability)
        => NextUnit() < probability;

    public double NextGaussian(double mean, double stdDev)
    {
        var u1 = Math.Max(double.Epsilon, NextUnit());
        var u2 = NextUnit();
        var radius = Math.Sqrt(-2.0 * Math.Log(u1));
        var z = radius * Math.Cos(2.0 * Math.PI * u2);
        return mean + stdDev * z;
    }

    /// <summary>
    /// Knuth's product method for small means, Gaussian approximation once that would underflow.
    /// </summary>
    public int NextPoisson(double mean)
    {
        if (mean <= 0.0)
        {
            return 0;
        }

        if (mean > 30.0)
        {
            return Math.Max(0, (int)Math.Round(NextGaussian(mean, Math.Sqrt(mean))));
        }

        var limit = Math.Exp(-mean);
        var product = NextUnit();
        var count = 0;
        while (product > limit)
        {
            count++;
            product *= NextUnit();
        }

        return count;
    }
}
