using AstraExtera.Galaxy;
using Vintagestory.API.Client;
using Vintagestory.API.Config;

namespace AstraExtera.Client;

/// <summary>
/// Opens the galaxy panel on Ctrl+Shift+S.
/// </summary>
/// <remarks>
/// The panel is built from the sky the server sent, so before that arrives there is nothing
/// to show and the player is told so rather than being given an empty window. The built panel is
/// kept and reopened; it is rebuilt when the cosmology seed changes, either after a server reroll
/// or when joining a different world in the same session.
/// </remarks>
public sealed class GalaxyPanelController
{
    public const string HotkeyCode = "astraextera-galaxypanel";

    private readonly ICoreClientAPI api;
    private readonly Func<GalaxySky?> currentSky;
    private GalaxyPanelDialog? dialog;

    public GalaxyPanelController(ICoreClientAPI api, Func<GalaxySky?> currentSky)
    {
        this.api = api;
        this.currentSky = currentSky;
    }

    public void Register()
    {
        api.Input.RegisterHotKey(
            HotkeyCode,
            Lang.Get("astraextera:hotkey-galaxypanel"),
            GlKeys.S,
            HotkeyType.GUIOrOtherControls,
            altPressed: false,
            ctrlPressed: true,
            shiftPressed: true);
        api.Input.SetHotKeyHandler(HotkeyCode, _ => Toggle());
    }

    private bool Toggle()
    {
        if (dialog?.IsOpened() == true)
        {
            dialog.TryClose();
            return true;
        }

        var sky = currentSky();
        if (sky is null)
        {
            api.TriggerIngameError(
                this,
                "astraextera-nogalaxy",
                "No galaxy yet: AstraExtera is still waiting for the server to send this world's placement.");
            return true;
        }

        if (dialog is null || dialog.WorldSeed != sky.Placement.WorldSeed)
        {
            dialog?.Dispose();
            dialog = new GalaxyPanelDialog(api, sky);
        }

        dialog.TryOpen();
        return true;
    }

    public void Dispose()
    {
        dialog?.Dispose();
        dialog = null;
    }
}
