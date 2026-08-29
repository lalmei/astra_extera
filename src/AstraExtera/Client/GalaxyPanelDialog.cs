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
/// The facts outrun any sensible dialog height -- a system with several comets adds rows -- so they
/// live in their own scrolling column while the figures stay put beside them.
/// </remarks>
public sealed class GalaxyPanelDialog : GuiDialog
{
    public const string ComposerName = "astraextera-galaxy-panel";

    private const string FactsKey = "astraextera-galaxy-facts";
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
    private readonly byte[] sky;
    private readonly double factsHeight;

    private ElementBounds factsBounds = null!;
    private double scrollOffset;

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
            .AddStaticCustomDraw(figuresBounds, OnDrawFigures)
            .Compose();

        // The scrollbar needs both heights in the same units the bounds use, so it knows how much of
        // the column is off-screen.
        SingleComposer.GetScrollbar(ScrollbarKey)
            .SetHeights((float)GalaxyPanelPainter.DesignHeight, (float)factsHeight);
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
