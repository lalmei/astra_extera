using AstraExtera.Galaxy;
using Cairo;
using Vintagestory.API.Client;

namespace AstraExtera.Client;

/// <summary>
/// The in-game galaxy panel: this world's placement written out, with the face-on, edge-on and
/// all-sky figures from the debug preview drawn alongside it.
/// </summary>
/// <remarks>
/// The star field and sky image are sampled once in the constructor and drawn from a static
/// composition afterwards. Neither depends on anything that changes while the panel is open, and
/// sampling a sky costs milliseconds rather than microseconds, so it has no business being on a
/// frame path.
/// </remarks>
public sealed class GalaxyPanelDialog : GuiDialog
{
    private const double TitleBarHeight = 31.0;

    private readonly GalaxyPlacement placement;
    private readonly StarField starField;
    private readonly byte[] sky;

    public GalaxyPanelDialog(ICoreClientAPI capi, GalaxyPlacement placement)
        : base(capi)
    {
        ArgumentNullException.ThrowIfNull(placement);
        this.placement = placement;
        starField = StarFieldSampler.Sample(placement);
        sky = GalaxySkyView.RenderRgb(placement, starField);
        Compose();
    }

    public override string ToggleKeyCombinationCode => GalaxyPanelController.HotkeyCode;

    public long WorldSeed => placement.WorldSeed;

    private void Compose()
    {
        var width = GalaxyPanelPainter.DesignWidth;
        var height = GalaxyPanelPainter.DesignHeight;

        var dialogBounds = ElementBounds.Fixed(
            EnumDialogArea.CenterMiddle,
            -width / 2.0,
            -(height + TitleBarHeight) / 2.0,
            width,
            height + TitleBarHeight);
        var canvasBounds = ElementBounds.Fixed(0, TitleBarHeight, width, height);

        SingleComposer = capi.Gui
            .CreateCompo("astraextera-galaxy-panel", dialogBounds)
            .AddShadedDialogBG(ElementBounds.Fill, true)
            .AddDialogTitleBar(GalaxyFacts.PanelTitle(placement), () => TryClose())
            .AddStaticCustomDraw(canvasBounds, OnDrawPanel)
            .Compose();
    }

    private void OnDrawPanel(Context ctx, ImageSurface surface, ElementBounds currentBounds)
        => GalaxyPanelPainter.Paint(ctx, currentBounds, placement, starField, sky);
}
