using AstraExtera.Galaxy;
using AstraTerra.Astronomy;
using AstraTerra.Client.Rendering;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;

namespace AstraExtera.Client;

/// <summary>
/// Draws the unresolved galactic glow as six cube faces behind AstraTerra's star billboards.
/// Vanilla's night cubemap is already suppressed, so without this pass the band of light is only
/// the preview PNG.
/// </summary>
public sealed class GalaxyGlowRenderer : IRenderer
{
    /// <summary>
    /// After vanilla night sky (0.1) and before AstraTerra's stars (0.3), with the depth test off
    /// so later opaque terrain still occludes it.
    /// </summary>
    public const double GlowRenderOrder = 0.2;

    private const float SkyDistance = 41.0f;
    private const int Subdivisions = SkyCubemap.DefaultSubdivisions;
    private static readonly int[] Indices = SkyCubemap.FaceIndices(Subdivisions);
    private static readonly SkyCubemapVertex[][] Grids = BuildGrids();

    private readonly ICoreClientAPI api;
    private readonly FacePass[] faces = new FacePass[6];
    private readonly float[] modelMatrix = IdentityModelMatrix();
    private GalaxyPlacement? pendingPlacement;
    private bool renderingDisabledAfterFailure;

    public GalaxyGlowRenderer(ICoreClientAPI api)
    {
        this.api = api;
        for (var i = 0; i < faces.Length; i++)
        {
            faces[i] = new FacePass(api);
        }
    }

    public double RenderOrder => GlowRenderOrder;

    public int RenderRange => 9999;

    public void Apply(GalaxyPlacement placement)
    {
        ArgumentNullException.ThrowIfNull(placement);
        pendingPlacement = placement;
        renderingDisabledAfterFailure = false;
    }

    public void OnRenderFrame(float deltaTime, EnumRenderStage stage)
    {
        if (renderingDisabledAfterFailure || stage != EnumRenderStage.Opaque)
        {
            return;
        }

        try
        {
            if (pendingPlacement is { } placement)
            {
                pendingPlacement = null;
                UploadFaces(placement);
            }

            if (!faces[0].HasTexture)
            {
                return;
            }

            RenderGlow();
        }
        catch (Exception exception)
        {
            renderingDisabledAfterFailure = true;
            api.Logger.Warning("AstraExtera disabled galaxy-glow rendering after an unexpected error: {0}", exception);
        }
    }

    public void Dispose()
    {
        for (var i = 0; i < faces.Length; i++)
        {
            faces[i].Dispose();
        }
    }

    private void UploadFaces(GalaxyPlacement placement)
    {
        var equatorial = GalaxySkyView.RenderEquatorialGlowRgb(placement);
        var rgbFaces = SkyCubemap.FromEquirectangular(equatorial, GalaxySkyView.Width, GalaxySkyView.Height);
        for (var i = 0; i < faces.Length; i++)
        {
            faces[i].UploadTexture(ToRgba(rgbFaces[i], SkyCubemap.FaceSize));
        }
    }

    private void RenderGlow()
    {
        var calendar = api.World.Calendar;
        var naturalDarkness = 1.0 - calendar.DayLightStrength;
        if (!SkyStarSunMoonRenderer.ShouldRenderForDarkness(
                naturalDarkness,
                SkyStarSunMoonRenderer.ForceDaylightStars))
        {
            return;
        }

        var darkness = SkyStarSunMoonRenderer.ForceDaylightStars ? 1.0 : naturalDarkness;
        var position = api.World.Player.Entity.Pos;
        var latitude = LatitudeMapper.MapGameLatitude(
            position.Z,
            calendar.OnGetLatitude is null ? null : z => calendar.OnGetLatitude(z));
        // The glow has to turn on exactly the longitude AstraTerra turns its stars on, or the band
        // slides out from behind the billboards it belongs to as the player travels east or west.
        // ObserverLongitude is that one answer: the world's own pole-to-equator scale, and zero
        // whenever the visible sun ignores longitude and the whole sky keeps a single solar time.
        var longitude = ObserverLongitude.ForObserver(position.X, api.World);
        var localSiderealAngle = CelestialMath.GetVanillaAlignedLocalSiderealAngle(
            calendar.TotalDays,
            Math.Max(1, calendar.DaysPerYear),
            Math.Max(1.0, calendar.HoursPerDay),
            longitude);

        var entity = api.World.Player.Entity;
        var verticalOrigin = (float)entity.LocalEyePos.Y
            - ((float)entity.Pos.Y - api.World.SeaLevel) / 10000f;
        modelMatrix[13] = verticalOrigin;

        var render = api.Render;
        var shader = render.StandardShader;
        var tint = new Vec4f(1f, 1f, 1f, (float)darkness);

        render.GlToggleBlend(true, EnumBlendMode.Standard);
        render.GlDisableCullFace();
        render.GLDisableDepthTest();
        render.GLDepthMask(false);
        try
        {
            shader.Use();
            shader.Uniform("skyShaded", 0);
            shader.RgbaAmbientIn = ColorUtil.WhiteRgbVec;
            shader.RgbaFogIn = render.FogColor;
            shader.ExtraGlow = 0;
            shader.FogMinIn = render.FogMin;
            shader.FogDensityIn = render.FogDensity;
            shader.DontWarpVertices = 0;
            shader.AddRenderFlags = 0;
            shader.ExtraZOffset = 0f;
            shader.NormalShaded = 0;
            shader.OverlayOpacity = 0f;
            shader.ExtraGodray = 0f;
            shader.AlphaTest = 0.01f;
            shader.ViewMatrix = render.CameraMatrixOriginf;
            shader.ProjectionMatrix = render.CurrentProjectionMatrix;
            shader.RgbaTint = tint;
            shader.RgbaLightIn = ColorUtil.WhiteArgbVec;
            ((IShaderProgram)shader).UniformMatrix("modelMatrix", modelMatrix);

            for (var i = 0; i < faces.Length; i++)
            {
                faces[i].Draw(shader, Grids[i], latitude, localSiderealAngle);
            }
        }
        finally
        {
            shader.Stop();
            render.GLDepthMask(true);
            render.GlToggleBlend(false, EnumBlendMode.Standard);
            render.GlEnableCullFace();
            render.GLEnableDepthTest();
        }
    }

    private static SkyCubemapVertex[][] BuildGrids()
    {
        var grids = new SkyCubemapVertex[6][];
        for (var i = 0; i < 6; i++)
        {
            grids[i] = SkyCubemap.FaceGrid((SkyCubeFace)i, Subdivisions);
        }

        return grids;
    }

    private static int[] ToRgba(byte[] rgb, int size)
    {
        var pixels = new int[size * size];
        for (var y = 0; y < size; y++)
        {
            for (var x = 0; x < size; x++)
            {
                var i = (y * size + x) * 3;
                pixels[(y * size) + x] = ColorUtil.ColorFromRgba(rgb[i], rgb[i + 1], rgb[i + 2], 255);
            }
        }

        return pixels;
    }

    private static float[] IdentityModelMatrix()
        =>
        [
            1f, 0f, 0f, 0f,
            0f, 1f, 0f, 0f,
            0f, 0f, 1f, 0f,
            0f, 0f, 0f, 1f
        ];

    private sealed class FacePass
    {
        private readonly ICoreClientAPI api;
        private LoadedTexture texture;
        private readonly MeshData meshData;
        private MeshRef? mesh;

        public FacePass(ICoreClientAPI api)
        {
            this.api = api;
            texture = new LoadedTexture(api)
            {
                Width = SkyCubemap.FaceSize,
                Height = SkyCubemap.FaceSize
            };
            meshData = new MeshData(
                Grids[0].Length,
                Indices.Length,
                withNormals: false,
                withUv: true,
                withRgba: true,
                withFlags: true)
            {
                mode = EnumDrawMode.Triangles
            };
        }

        public bool HasTexture => texture.TextureId != 0;

        public void UploadTexture(int[] pixels)
        {
            texture.Width = SkyCubemap.FaceSize;
            texture.Height = SkyCubemap.FaceSize;
            api.Render.LoadOrUpdateTextureFromRgba(pixels, true, 0, ref texture);
        }

        public void Draw(
            IStandardShaderProgram shader,
            SkyCubemapVertex[] grid,
            double latitudeDeg,
            double localSiderealDeg)
        {
            if (texture.TextureId == 0)
            {
                return;
            }

            FillMesh(grid, latitudeDeg, localSiderealDeg);
            if (mesh is null)
            {
                mesh = api.Render.UploadMesh(meshData);
            }
            else
            {
                api.Render.UpdateMesh(mesh, meshData);
            }

            shader.Tex2D = texture.TextureId;
            api.Render.RenderMesh(mesh);
        }

        public void Dispose()
        {
            mesh?.Dispose();
            mesh = null;
            texture.Dispose();
        }

        private void FillMesh(SkyCubemapVertex[] grid, double latitudeDeg, double localSiderealDeg)
        {
            meshData.VerticesCount = 0;
            meshData.IndicesCount = 0;

            foreach (var vertex in grid)
            {
                var (x, y, z) = SkyProjection.GetWorldDirection(
                    new EquatorialCoordinates(vertex.RightAscensionDeg, vertex.DeclinationDeg),
                    latitudeDeg,
                    localSiderealDeg);
                meshData.AddVertexWithFlags(
                    (float)x * SkyDistance,
                    (float)y * SkyDistance,
                    (float)z * SkyDistance,
                    vertex.U,
                    1f - vertex.V,
                    ColorUtil.WhiteArgb,
                    0);
            }

            foreach (var index in Indices)
            {
                meshData.AddIndex(index);
            }
        }
    }
}
