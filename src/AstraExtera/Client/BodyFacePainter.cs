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
    /// <summary>A parent giant fills a good part of the sky, so its face carries real detail.</summary>
    public const int GiantFaceSize = 512;

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
            GiantFaceSize);

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

    private static NearBodyFace ToFace(CelestialFace face)
        => new(face.Size, face.Pixels, face.DiscFraction);
}
