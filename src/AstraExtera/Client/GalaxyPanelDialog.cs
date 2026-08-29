using AstraExtera.Galaxy;
using Cairo;
using Vintagestory.API.Client;

namespace AstraExtera.Client;

/// <summary>
/// The in-game galaxy panel: this world's placement written out, with the face-on, edge-on and
/// all-sky figures from the debug preview drawn alongside it. It uses the same dialog shell as
/// other Vintage Story windows, so the title bar has close and movable/fixed, and a dragged
/// position is remembered.
/// </summary>
/// <remarks>
/// The star field and sky image are the server-stored catalog, drawn from a static composition
/// afterwards. Neither depends on anything that changes while the panel is open, so sampling has
/// no business being on a frame path -- and the client does not sample at all.
/// </remarks>
public sealed class GalaxyPanelDialog : GuiDialog
{
    public const string ComposerName = "astraextera-galaxy-panel";

    private readonly GalaxyPlacement placement;
    private readonly StarField starField;
    private readonly LocalSystemSky localSky;
    private readonly byte[] sky;

    public GalaxyPanelDialog(ICoreClientAPI capi, GalaxySky authored)
        : base(capi)
    {
        ArgumentNullException.ThrowIfNull(authored);
        placement = authored.Placement;
        starField = authored.StarField;
        localSky = authored.LocalSky;
        sky = GalaxySkyView.RenderRgb(placement, starField);
        Compose();
    }

    public override string ToggleKeyCombinationCode => GalaxyPanelController.HotkeyCode;

    public long WorldSeed => placement.WorldSeed;

    private void Compose()
    {
        // Same shell as vanilla windows: autosized, title bar with close and movable/fixed,
        // position remembered under the composer name. Center-bottom so the sky stays visible
        // above it rather than sitting over the middle of the view.
        var canvasBounds = ElementBounds.Fixed(
            0,
            GuiStyle.TitleBarHeight,
            GalaxyPanelPainter.DesignWidth,
            GalaxyPanelPainter.DesignHeight);
        var backgroundBounds = ElementStdBounds.DialogBackground()
            .WithChildren(canvasBounds);
        var dialogBounds = ElementStdBounds.AutosizedMainDialog
            .WithAlignment(EnumDialogArea.CenterBottom)
            .WithFixedAlignmentOffset(0, -GuiStyle.DialogToScreenPadding);

        SingleComposer = capi.Gui
            .CreateCompo(ComposerName, dialogBounds)
            .AddShadedDialogBG(backgroundBounds, true)
            .AddDialogTitleBar(GalaxyFacts.PanelTitle(placement), () => TryClose())
            .AddStaticCustomDraw(canvasBounds, OnDrawPanel)
            .Compose();
    }

    private void OnDrawPanel(Context ctx, ImageSurface surface, ElementBounds currentBounds)
        => GalaxyPanelPainter.Paint(ctx, currentBounds, placement, starField, sky, localSky);
}
