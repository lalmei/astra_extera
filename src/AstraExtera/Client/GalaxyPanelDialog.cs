using AstraExtera.Galaxy;
using Cairo;
using Vintagestory.API.Client;

namespace AstraExtera.Client;

/// <summary>
/// The in-game galaxy panel: this world's placement written out, with the face-on, edge-on, all-sky
/// and local-system figures from the debug preview drawn alongside it. It uses the same dialog shell
/// as other Vintage Story windows, so the title bar has close and movable/fixed, and a dragged
/// position is remembered.
/// </summary>
/// <remarks>
/// <para>
/// The facts outrun any sensible dialog height -- a system with several comets adds rows -- so they
/// live in their own scrolling column while the figures stay put beside them.
/// </para>
/// <para>
/// Both columns are dynamic custom draws. A static one is composed onto the dialog's own surface,
/// where the drawing has to place itself; a dynamic one gets a texture of its own with its origin at
/// the element, which is the frame both painters draw in.
/// </para>
/// <para>
/// The all-sky panorama is marched off the main thread, because it is seconds of work and the game
/// loop is not the place for it. The panel opens straight away with that one box saying so, and
/// redraws itself when the render lands.
/// </para>
/// </remarks>
public sealed class GalaxyPanelDialog : GuiDialog
{
    public const string ComposerName = "astraextera-galaxy-panel";

    private const string FactsKey = "astraextera-galaxy-facts";
    private const string FiguresKey = "astraextera-galaxy-figures";
    private const string ScrollbarKey = "astraextera-galaxy-scroll";

    /// <summary>
    /// Clearance between the top of the dialog and its first content. The title bar itself is
    /// <see cref="GuiStyle.TitleBarHeight"/>, but a child placed at exactly that height still lands
    /// under the title text; vanilla dialogs leave the same margin this does.
    /// </summary>
    private static readonly double TitleClearance = GuiStyle.TitleBarHeight + 14.0;

    private readonly GalaxyPlacement placement;
    private readonly StarField starField;
    private readonly LocalSystemSky localSky;
    private readonly double factsHeight;

    private ElementBounds factsBounds = null!;
    private double scrollOffset;
    private byte[]? sky;
    private bool disposed;

    public GalaxyPanelDialog(ICoreClientAPI capi, GalaxySky authored)
        : base(capi)
    {
        ArgumentNullException.ThrowIfNull(authored);
        placement = authored.Placement;
        starField = authored.StarField;
        localSky = authored.LocalSky;
        sky = GalaxySkyView.RenderRgb(placement, starField);
        factsHeight = GalaxyPanelPainter.MeasureFactsHeight(placement, starField, localSky);
        Compose();
    }

    public override string ToggleKeyCombinationCode => GalaxyPanelController.HotkeyCode;

    public long WorldSeed => placement.WorldSeed;

    private void Compose()
    {
        // Same shell as vanilla windows: autosized, title bar with close and movable/fixed, position
        // remembered under the composer name. Center-bottom so the sky stays visible above it.
        factsBounds = ElementBounds.Fixed(
            0,
            TitleClearance,
            GalaxyPanelPainter.FactsWidth,
            GalaxyPanelPainter.DesignHeight);

        var scrollbarBounds = ElementBounds.Fixed(
            GalaxyPanelPainter.FactsWidth + 4.0,
            TitleClearance,
            GalaxyPanelPainter.ScrollbarWidth,
            GalaxyPanelPainter.DesignHeight);

        var figuresBounds = ElementBounds.Fixed(
            GalaxyPanelPainter.FactsWidth + GalaxyPanelPainter.ScrollbarWidth + GalaxyPanelPainter.ColumnGap,
            TitleClearance,
            GalaxyPanelPainter.FiguresWidth,
            GalaxyPanelPainter.DesignHeight);

        var backgroundBounds = ElementStdBounds.DialogBackground()
            .WithChildren(factsBounds, scrollbarBounds, figuresBounds);
        var dialogBounds = ElementStdBounds.AutosizedMainDialog
            .WithAlignment(EnumDialogArea.CenterBottom)
            .WithFixedAlignmentOffset(0, -GuiStyle.DialogToScreenPadding);

        SingleComposer = capi.Gui
            .CreateCompo(ComposerName, dialogBounds)
            .AddShadedDialogBG(backgroundBounds, true)
            .AddDialogTitleBar(GalaxyFacts.PanelTitle(placement), () => TryClose())
            .AddDynamicCustomDraw(factsBounds, OnDrawFacts, FactsKey)
            .AddVerticalScrollbar(OnScroll, scrollbarBounds, ScrollbarKey)
            .AddDynamicCustomDraw(figuresBounds, OnDrawFigures, FiguresKey)
            .Compose();

        // The scrollbar needs both heights in the same units the bounds use, so it knows how much of
        // the column is off-screen.
        SingleComposer.GetScrollbar(ScrollbarKey)
            .SetHeights((float)GalaxyPanelPainter.DesignHeight, (float)factsHeight);
    }

    /// <summary>
    /// Marches the all-sky glow on a worker thread and hands the result back to the main thread,
    /// which is the only one allowed to touch the composer.
    /// </summary>
    private void BeginSkyRender()
    {
        var forPlacement = placement;
        var forStars = starField;
        Task.Run(() =>
        {
            byte[] rendered;
            try
            {
                rendered = GalaxySkyView.RenderRgb(forPlacement, forStars);
            }
            catch (Exception exception)
            {
                capi.Logger.Warning("AstraExtera could not render the galaxy panel's all-sky view: {0}", exception);
                return;
            }

            capi.Event.EnqueueMainThreadTask(() => OnSkyRendered(rendered), "astraextera-galaxy-sky");
        });
    }

    private void OnSkyRendered(byte[] rendered)
    {
        if (disposed)
        {
            return;
        }

        sky = rendered;
        SingleComposer?.GetCustomDraw(FiguresKey)?.Redraw();
    }

    private void OnScroll(float value)
    {
        scrollOffset = value;
        SingleComposer.GetCustomDraw(FactsKey).Redraw();
    }

    private void OnDrawFacts(Context ctx, ImageSurface surface, ElementBounds currentBounds)
        => GalaxyPanelPainter.PaintFacts(ctx, currentBounds, placement, starField, localSky, scrollOffset);

    private void OnDrawFigures(Context ctx, ImageSurface surface, ElementBounds currentBounds)
        => GalaxyPanelPainter.PaintFigures(ctx, currentBounds, placement, sky, localSky);
}
