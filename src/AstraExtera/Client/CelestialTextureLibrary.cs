using AstraExtera.Galaxy;
using Vintagestory.API.Client;
using Vintagestory.API.Common;

namespace AstraExtera.Client;

/// <summary>
/// Loads the shipped celestial artwork and hands it out as plain pixels.
/// </summary>
/// <remarks>
/// <para>
/// The pictures are read once and kept: a save uses a handful of them and the same giant is painted
/// again whenever a player rejoins.
/// </para>
/// <para>
/// Two conversions happen here and nowhere else. The game hands a decoded bitmap back as
/// premultiplied ARGB, and everything downstream -- the compositor, the texture upload -- works in
/// straight RGBA. Getting either wrong is quiet rather than loud: swapped channels give a blue
/// Jupiter, and left-premultiplied pixels give rings that darken as they fade.
/// </para>
/// </remarks>
public sealed class CelestialTextureLibrary
{
    private readonly ICoreClientAPI api;
    private readonly Dictionary<string, CelestialSource> loaded = new(StringComparer.Ordinal);
    private CelestialTextureManifest? manifest;

    public CelestialTextureLibrary(ICoreClientAPI api)
    {
        this.api = api;
    }

    public CelestialTextureManifest Manifest => manifest ??= LoadManifest();

    public CelestialSource? Load(CelestialTexture? texture)
    {
        if (texture is null)
        {
            return null;
        }

        if (loaded.TryGetValue(texture.Id, out var cached))
        {
            return cached;
        }

        try
        {
            var asset = api.Assets.TryGet(new AssetLocation(CelestialTextureManifest.TextureFolder + texture.File));
            if (asset is null)
            {
                api.Logger.Warning("AstraExtera could not find the celestial texture {0}.", texture.File);
                return null;
            }

            using var bitmap = asset.ToBitmap(api);
            if (bitmap.Width != bitmap.Height)
            {
                api.Logger.Warning("AstraExtera ignored the celestial texture {0}: it is not square.", texture.File);
                return null;
            }

            var source = new CelestialSource(bitmap.Width, ToStraightRgba(bitmap.Pixels), texture.DiscFraction);
            loaded[texture.Id] = source;
            return source;
        }
        catch (Exception exception)
        {
            api.Logger.Warning("AstraExtera could not read the celestial texture {0}: {1}", texture.File, exception);
            return null;
        }
    }

    private CelestialTextureManifest LoadManifest()
    {
        try
        {
            var asset = api.Assets.TryGet(new AssetLocation(CelestialTextureManifest.AssetPath));
            if (asset is not null)
            {
                var read = CelestialTextureManifest.FromUtf8(asset.Data);
                api.Logger.Event(
                    "AstraExtera loaded {0} celestial textures: {1} giants, {2} moons, {3} rings.",
                    read.Textures.Count,
                    read.Giants.Count(),
                    read.Moons.Count(),
                    read.Rings.Count());
                return read;
            }

            api.Logger.Warning("AstraExtera found no celestial texture manifest; bodies will be drawn flat.");
        }
        catch (Exception exception)
        {
            api.Logger.Warning("AstraExtera could not read the celestial texture manifest: {0}", exception);
        }

        return CelestialTextureManifest.Empty;
    }

    /// <summary>
    /// Premultiplied ARGB, as the game's decoder returns it, into the straight RGBA the rest of the
    /// mod uses.
    /// </summary>
    internal static int[] ToStraightRgba(int[] argb)
    {
        var rgba = new int[argb.Length];
        for (var i = 0; i < argb.Length; i++)
        {
            var pixel = argb[i];
            var alpha = (pixel >> 24) & 0xFF;
            if (alpha == 0)
            {
                continue;
            }

            var red = (pixel >> 16) & 0xFF;
            var green = (pixel >> 8) & 0xFF;
            var blue = pixel & 0xFF;
            if (alpha < 255)
            {
                red = Math.Min(255, red * 255 / alpha);
                green = Math.Min(255, green * 255 / alpha);
                blue = Math.Min(255, blue * 255 / alpha);
            }

            rgba[i] = CelestialFaceComposer.Pack(red, green, blue, alpha);
        }

        return rgba;
    }
}
