using AstraExtera.Galaxy;
using AstraTerra.Astronomy;

namespace AstraExtera.Client;

/// <summary>
/// Builds the face a near body is drawn with: the parent giant a moon world hangs beneath, and the
/// sibling moons crossing that sky.
/// </summary>
/// <remarks>
/// <para>
/// The picture comes from the shipped artwork rather than from anything drawn here. What the
/// generator authored still decides everything about it: which world is chosen -- the one whose
/// colour is nearest the cloud decks it asked for -- how far the ring reaches, how open it is, and
/// which way its line runs.
/// </para>
/// <para>
/// If the artwork is missing the body still gets a face, flat and in its authored colour, because a
/// sky with a hole where the planet should be is worse than a plain disc.
/// </para>
/// </remarks>
public static class BodyFacePainter
{
    /// <summary>Smallest face a giant gets, and the size of one with no rings to make room for.</summary>
    public const int GiantFaceSize = 512;

    /// <summary>
    /// Largest face a giant gets. A ringed giant's globe is only a fraction of its face -- the rest
    /// is the room the rings need -- so the face has to be several times the globe's own picture or
    /// the globe is thrown away before it is ever drawn.
    /// </summary>
    public const int MaxGiantFaceSize = 2048;

    /// <summary>A sibling moon is a few degrees at most; more pixels than this would never be seen.</summary>
    public const int MoonFaceSize = 128;

    /// <param name="ringOpenness">
    /// How far open the rings look from where this body is being watched, as the ellipse's short axis
    /// over its long one. It is the caller's to work out, not the giant's: the same rings are wide
    /// open from another planet and a knife edge from a moon that orbits inside their plane.
    /// </param>
    public static NearBodyFace PaintGiant(
        CelestialTextureLibrary library,
        GiantAppearance? appearance,
        double ringOpenness,
        long worldSeed)
    {
        ArgumentNullException.ThrowIfNull(library);
        var manifest = library.Manifest;
        var globe = library.Load(manifest.PickGiant(appearance, worldSeed));
        if (globe is null)
        {
            return ToFace(CelestialFaceComposer.Flat(GiantFaceSize, 0.78f, 0.68f, 0.52f));
        }

        var ringRecord = manifest.PickRing(appearance?.Ring, worldSeed);
        var face = CelestialFaceComposer.Compose(
            globe,
            library.Load(ringRecord),
            ringRecord,
            appearance?.Ring,
            ringOpenness,
            appearance is null ? 0.0 : GiantAppearances.RingRollRadians(appearance),
            FaceSizeFor(globe.Size, appearance?.Ring));

        return ToFace(face);
    }

    public static NearBodyFace PaintMoon(CelestialTextureLibrary library, SystemMoon moon, long worldSeed)
    {
        ArgumentNullException.ThrowIfNull(library);
        ArgumentNullException.ThrowIfNull(moon);

        var globe = library.Load(library.Manifest.PickMoon(worldSeed, moon.Index));
        return globe is null
            ? ToFace(CelestialFaceComposer.Flat(MoonFaceSize, 0.72f, 0.70f, 0.67f))
            : ToFace(CelestialFaceComposer.Compose(
                globe,
                ring: null,
                ringRecord: null,
                authoredRing: null,
                openness: 0.0,
                rollRadians: 0.0,
                MoonFaceSize));
    }

    /// <summary>
    /// How big the composed face has to be for the globe to keep every pixel its own picture has.
    /// Rings push the globe into the middle of the face, so the face grows with their reach.
    /// </summary>
    public static int FaceSizeFor(int globeSourceSize, PlanetRing? ring)
    {
        var reach = Math.Max(1.0, ring?.OuterRadiusPlanetRadii ?? 1.0);
        var wanted = globeSourceSize * reach;
        var size = GiantFaceSize;
        while (size < wanted && size < MaxGiantFaceSize)
        {
            size *= 2;
        }

        return size;
    }

    private static NearBodyFace ToFace(CelestialFace face)
        => new(face.Size, face.Pixels, face.DiscFraction);
}
