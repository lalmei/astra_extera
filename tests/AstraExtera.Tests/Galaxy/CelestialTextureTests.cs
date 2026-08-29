using AstraExtera.Galaxy;
using Xunit;

namespace AstraExtera.Tests.Galaxy;

/// <summary>
/// The shipped artwork, its manifest, and the compositor that turns a picture plus an authored ring
/// into the face a near body is drawn with.
/// </summary>
public sealed class CelestialTextureTests
{
    /// <summary>Where the mod's own copy of the artwork lives, relative to the repository.</summary>
    private static readonly string AssetRoot = FindAssetRoot();

    [Fact]
    public void The_Shipped_Manifest_Describes_Every_Shipped_Texture()
    {
        var manifest = LoadManifest();

        Assert.Equal(CelestialTextureManifest.CurrentSchemaVersion, manifest.SchemaVersion);
        Assert.NotEmpty(manifest.Giants);
        Assert.NotEmpty(manifest.Moons);
        Assert.NotEmpty(manifest.Rings);

        foreach (var texture in manifest.Textures)
        {
            Assert.True(
                File.Exists(Path.Combine(AssetRoot, "textures", "celestial", texture.File)),
                $"{texture.File} is in the manifest but not shipped");
            Assert.InRange(texture.Red, 0.0f, 1.0f);
            Assert.InRange(texture.Green, 0.0f, 1.0f);
            Assert.InRange(texture.Blue, 0.0f, 1.0f);
            Assert.InRange(texture.DiscFraction, 0.1, 1.0);
        }

        foreach (var ring in manifest.Rings)
        {
            Assert.InRange(ring.BakedOpenness, 0.02, 0.9);
            Assert.InRange(ring.OuterRadiusFraction, 0.2, 0.5);
            Assert.True(ring.InnerRadiusFraction < ring.OuterRadiusFraction);
        }
    }

    /// <summary>
    /// The artwork is fixed and the generator is not, so the match has to be made on colour: a giant
    /// authored with blue methane decks must not come back as Jupiter.
    /// </summary>
    [Fact]
    public void A_Giant_Gets_The_Picture_Nearest_The_Colour_It_Was_Authored()
    {
        var manifest = LoadManifest();
        var blue = manifest.PickGiant(Face(0.55f, 0.70f, 0.95f, 0.15f, 0.35f, 0.72f), 42);
        var tan = manifest.PickGiant(Face(0.95f, 0.88f, 0.72f, 0.62f, 0.40f, 0.24f), 42);

        Assert.NotNull(blue);
        Assert.NotNull(tan);
        Assert.True(blue.Blue > blue.Red, $"{blue.Id} is not a blue world");
        Assert.True(tan.Red > tan.Blue, $"{tan.Id} is not a warm world");
        Assert.NotEqual(blue.Id, tan.Id);

        // Same seed, same save, same world every time.
        Assert.Equal(blue.Id, manifest.PickGiant(Face(0.55f, 0.70f, 0.95f, 0.15f, 0.35f, 0.72f), 42)!.Id);
    }

    [Fact]
    public void A_Sooty_Ring_Does_Not_Come_Back_As_A_Sheet_Of_Ice()
    {
        var manifest = LoadManifest();
        var ice = manifest.PickRing(Ring(RingComposition.Ice, 0.95f, 0.95f, 0.97f), 7);
        var soot = manifest.PickRing(Ring(RingComposition.Soot, 0.40f, 0.36f, 0.34f), 7);

        Assert.NotNull(ice);
        Assert.NotNull(soot);
        Assert.True(ice.Red + ice.Green + ice.Blue > soot.Red + soot.Green + soot.Blue);
        Assert.Null(manifest.PickRing(null, 7));
    }

    [Fact]
    public void Every_Moon_Picture_Is_A_Real_One_And_The_Same_Moon_Keeps_Its_Own()
    {
        var manifest = LoadManifest();
        var first = manifest.PickMoon(42, 1);
        var second = manifest.PickMoon(42, 2);

        Assert.NotNull(first);
        Assert.NotNull(second);
        Assert.Equal("moon", first.Kind);
        Assert.Equal(first.Id, manifest.PickMoon(42, 1)!.Id);
        Assert.NotEqual(first.Id, manifest.PickMoon(43, 1)!.Id);
    }

    /// <summary>
    /// The whole point of the compositor: a ringed giant's face is wider than its globe, the ring
    /// runs at the tilt the giant was given, and the globe sits in front of half of it.
    /// </summary>
    [Fact]
    public void A_Ringed_Face_Puts_The_Globe_Inside_Its_Rings()
    {
        var manifest = LoadManifest();
        var giant = manifest.Giants.First();
        var ringRecord = manifest.Rings.First();
        var ring = new PlanetRing(1.4, 2.6, 0.85, 0.0, RingComposition.Ice, 0.95f, 0.95f, 0.97f);

        var face = CelestialFaceComposer.Compose(
            Source(giant),
            Source(ringRecord),
            ringRecord,
            ring,
            openness: 0.45,
            rollRadians: 0.0,
            size: 256);

        Assert.Equal(256, face.Size);
        Assert.Equal(256 * 256, face.Pixels.Length);

        // The globe fills its share of the face and no more.
        Assert.Equal(CelestialFaceComposer.FaceMargin / ring.OuterRadiusPlanetRadii, face.DiscFraction, 6);

        var centre = 128;
        var globeRadius = (int)(128 * face.DiscFraction);
        Assert.Equal(255, Alpha(face, centre, centre));
        Assert.Equal(0, Alpha(face, 2, 2));

        // Ring pixels outside the globe, on the ring's long axis.
        var ringPixels = 0;
        for (var x = centre + globeRadius + 4; x < 250; x++)
        {
            if (Alpha(face, x, centre) > 0)
            {
                ringPixels++;
            }
        }

        Assert.True(ringPixels > 0, "the ring drew nothing beyond the globe");

        // Straight up from the centre is inside the globe, then empty sky: an open ring is an
        // ellipse, so it must not reach as far up the face as it does across it.
        Assert.Equal(0, Alpha(face, centre, 4));
    }

    [Fact]
    public void An_Untilted_Ring_Collapses_To_A_Line_And_A_Ringless_Giant_Fills_Its_Face()
    {
        var manifest = LoadManifest();
        var giant = manifest.Giants.First();
        var ringRecord = manifest.Rings.First();
        var ring = new PlanetRing(1.4, 2.6, 0.85, 0.0, RingComposition.Ice, 0.95f, 0.95f, 0.97f);

        var edgeOn = CelestialFaceComposer.Compose(
            Source(giant), Source(ringRecord), ringRecord, ring, openness: 0.0, rollRadians: 0.0, size: 128);
        var open = CelestialFaceComposer.Compose(
            Source(giant), Source(ringRecord), ringRecord, ring, openness: 0.6, rollRadians: 0.0, size: 128);

        Assert.True(Painted(edgeOn) < Painted(open), "an edge-on ring should cover less than an open one");

        var bare = CelestialFaceComposer.Compose(
            Source(giant), null, null, null, openness: 0.0, rollRadians: 0.0, size: 128);
        Assert.Equal(CelestialFaceComposer.FaceMargin, bare.DiscFraction, 6);
        Assert.Equal(255, Alpha(bare, 64, 64));
    }

    [Fact]
    public void A_Body_With_No_Artwork_Still_Gets_A_Face()
    {
        var flat = CelestialFaceComposer.Flat(64, 0.8f, 0.5f, 0.2f);

        Assert.Equal(255, Alpha(flat, 32, 32));
        Assert.Equal(204, CelestialFaceComposer.Red(flat.Pixels[(32 * 64) + 32]));
        Assert.Equal(0, Alpha(flat, 0, 0));
    }

    /// <summary>
    /// Writes a real composed face out where it can be looked at. Asserting a picture is pretty is
    /// not a thing a test can do, so this only checks it was drawn at all.
    /// </summary>
    [Fact]
    public void A_Composed_Face_Can_Be_Written_Out_For_Inspection()
    {
        var manifest = LoadManifest();
        var giant = manifest.PickGiant(Face(0.95f, 0.88f, 0.72f, 0.62f, 0.40f, 0.24f), 42)!;
        var ringRecord = manifest.PickRing(Ring(RingComposition.Ice, 0.95f, 0.95f, 0.97f), 42)!;
        var ring = new PlanetRing(1.35, 2.9, 0.9, 2.1, RingComposition.Ice, 0.95f, 0.95f, 0.97f);

        var face = CelestialFaceComposer.Compose(
            Source(giant), Source(ringRecord), ringRecord, ring, openness: 0.42, rollRadians: -0.35, size: 512);

        var output = Environment.GetEnvironmentVariable("ASTRAEXTERA_FACE_DUMP");
        if (!string.IsNullOrEmpty(output))
        {
            WriteRgbPng(output, face);
        }

        Assert.True(Painted(face) > 20_000, "the composed face came out nearly empty");
    }

    private static void WriteRgbPng(string path, CelestialFace face)
    {
        var rgb = new byte[face.Size * face.Size * 3];
        for (var i = 0; i < face.Pixels.Length; i++)
        {
            var pixel = face.Pixels[i];
            var alpha = CelestialFaceComposer.Alpha(pixel) / 255.0;
            rgb[i * 3] = (byte)(CelestialFaceComposer.Red(pixel) * alpha + (11 * (1 - alpha)));
            rgb[(i * 3) + 1] = (byte)(CelestialFaceComposer.Green(pixel) * alpha + (16 * (1 - alpha)));
            rgb[(i * 3) + 2] = (byte)(CelestialFaceComposer.Blue(pixel) * alpha + (32 * (1 - alpha)));
        }

        File.WriteAllBytes(path, RgbPng.Encode(face.Size, face.Size, rgb));
    }

    private static int Painted(CelestialFace face)
        => face.Pixels.Count(static pixel => CelestialFaceComposer.Alpha(pixel) > 8);

    private static int Alpha(CelestialFace face, int x, int y)
        => CelestialFaceComposer.Alpha(face.Pixels[(y * face.Size) + x]);

    private static CelestialSource Source(CelestialTexture texture)
    {
        var (size, pixels) = PngReader.ReadSquare(Path.Combine(AssetRoot, "textures", "celestial", texture.File));
        return new CelestialSource(size, pixels, texture.DiscFraction);
    }

    private static CelestialTextureManifest LoadManifest()
        => CelestialTextureManifest.FromUtf8(
            File.ReadAllBytes(Path.Combine(AssetRoot, "config", "celestial-textures.json")));

    private static GiantAppearance Face(float lightR, float lightG, float lightB, float darkR, float darkG, float darkB)
        => new(15.0, false, 10.0, 60.0, 9, lightR, lightG, lightB, darkR, darkG, darkB, null, null);

    private static PlanetRing Ring(RingComposition composition, float red, float green, float blue)
        => new(1.3, 2.4, 0.7, 0.0, composition, red, green, blue);

    private static string FindAssetRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            var candidate = Path.Combine(directory.FullName, "assets", "astraextera");
            if (Directory.Exists(candidate))
            {
                return candidate;
            }

            directory = directory.Parent;
        }

        throw new DirectoryNotFoundException("Could not find assets/astraextera above the test output.");
    }
}
