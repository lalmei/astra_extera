using System.Text.Json;
using System.Text.Json.Serialization;

namespace AstraExtera.Galaxy;

/// <summary>
/// One shipped picture of a celestial body, and what the preparation step measured about it.
/// </summary>
/// <param name="Kind">"giant", "moon" or "ring".</param>
/// <param name="DiscFraction">The body's disc as a fraction of the texture's half-width.</param>
/// <param name="OuterRadiusFraction">Rings only: the ellipse's long semi-axis, in the same terms.</param>
/// <param name="BakedOpenness">
/// Rings only: how open the ellipse was drawn, as its short axis over its long one. The mod squashes
/// the picture from this to whatever tilt the giant it belongs to actually has.
/// </param>
public sealed record CelestialTexture(
    string Id,
    string File,
    string Kind,
    float Red,
    float Green,
    float Blue,
    double DiscFraction,
    double OuterRadiusFraction,
    double InnerRadiusFraction,
    double BakedOpenness);

/// <summary>
/// The shipped celestial artwork, and which picture belongs to which authored body.
/// </summary>
/// <remarks>
/// <para>
/// The generator authors a giant before anything has been drawn: a mass, an obliquity, cloud-deck
/// colours, a ring of a certain composition. The artwork is fixed and finite. This is the seam
/// between them -- it picks the picture whose own colour is nearest what the generator asked for, so
/// an ice giant authored deep blue gets a blue world rather than a random one, and a save always
/// gets the same picture for the same body.
/// </para>
/// <para>
/// Pure: the manifest is JSON and the choice is arithmetic, so both can be tested without a game.
/// Loading the pictures themselves is the client's job.
/// </para>
/// </remarks>
public sealed record CelestialTextureManifest(int SchemaVersion, IReadOnlyList<CelestialTexture> Textures)
{
    public const int CurrentSchemaVersion = 1;

    /// <summary>Where the mod's own copy lives, for the client to load.</summary>
    public const string AssetPath = "astraextera:config/celestial-textures.json";

    public const string TextureFolder = "astraextera:textures/celestial/";

    public static CelestialTextureManifest Empty { get; } = new(CurrentSchemaVersion, []);

    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        PropertyNameCaseInsensitive = true,
        Converters = { new JsonStringEnumConverter() }
    };

    public IEnumerable<CelestialTexture> Giants => Textures.Where(static texture => texture.Kind == "giant");

    public IEnumerable<CelestialTexture> Moons => Textures.Where(static texture => texture.Kind == "moon");

    public IEnumerable<CelestialTexture> Rings => Textures.Where(static texture => texture.Kind == "ring");

    public static CelestialTextureManifest FromUtf8(byte[] utf8)
        => JsonSerializer.Deserialize<CelestialTextureManifest>(utf8, Options) ?? Empty;

    /// <summary>
    /// The picture for a giant the generator authored: the one whose colour sits closest to the
    /// cloud decks it was given, with the seed breaking ties so a save keeps the same world.
    /// </summary>
    public CelestialTexture? PickGiant(GiantAppearance? appearance, long seed)
    {
        var wanted = appearance is null
            ? (0.62f, 0.55f, 0.45f)
            : (
                (appearance.BandLightR * 0.6f) + (appearance.BandDarkR * 0.4f),
                (appearance.BandLightG * 0.6f) + (appearance.BandDarkG * 0.4f),
                (appearance.BandLightB * 0.6f) + (appearance.BandDarkB * 0.4f));

        return PickNearest(Giants, wanted, seed);
    }

    /// <summary>
    /// The picture for a ring: matched on the colour its composition implies, so sooted debris does
    /// not come back as a sheet of bright ice.
    /// </summary>
    public CelestialTexture? PickRing(PlanetRing? ring, long seed)
    {
        if (ring is null)
        {
            return null;
        }

        return PickNearest(Rings, (ring.TintR, ring.TintG, ring.TintB), seed);
    }

    /// <summary>A moon's picture. Nothing authored about a moon says what it should look like, so
    /// this only has to be stable and varied.</summary>
    public CelestialTexture? PickMoon(long seed, int index)
    {
        var moons = Moons.OrderBy(static texture => texture.Id, StringComparer.Ordinal).ToList();
        if (moons.Count == 0)
        {
            return null;
        }

        var rng = new SplitMix64(seed + (index * 104_729L));
        return moons[rng.NextInt(moons.Count)];
    }

    /// <summary>
    /// Nearest by colour, weighted toward hue rather than brightness -- a texture's overall
    /// lightness says more about how it was lit than about what it is.
    /// </summary>
    private static CelestialTexture? PickNearest(
        IEnumerable<CelestialTexture> candidates,
        (float Red, float Green, float Blue) wanted,
        long seed)
    {
        var ordered = candidates.OrderBy(static texture => texture.Id, StringComparer.Ordinal).ToList();
        if (ordered.Count == 0)
        {
            return null;
        }

        var best = double.MaxValue;
        var matches = new List<CelestialTexture>();
        foreach (var texture in ordered)
        {
            var distance = ColourDistance(wanted, (texture.Red, texture.Green, texture.Blue));
            if (distance < best - 1e-6)
            {
                best = distance;
                matches.Clear();
                matches.Add(texture);
            }
            else if (distance <= best + 0.01)
            {
                matches.Add(texture);
            }
        }

        var rng = new SplitMix64(seed);
        return matches[rng.NextInt(matches.Count)];
    }

    private static double ColourDistance(
        (float Red, float Green, float Blue) left,
        (float Red, float Green, float Blue) right)
    {
        var leftSum = Math.Max(1e-4f, left.Red + left.Green + left.Blue);
        var rightSum = Math.Max(1e-4f, right.Red + right.Green + right.Blue);
        var hue = Squared((left.Red / leftSum) - (right.Red / rightSum))
                  + Squared((left.Green / leftSum) - (right.Green / rightSum))
                  + Squared((left.Blue / leftSum) - (right.Blue / rightSum));
        var lightness = Squared((leftSum - rightSum) / 3.0);
        return (hue * 9.0) + lightness;
    }

    private static double Squared(double value) => value * value;
}
