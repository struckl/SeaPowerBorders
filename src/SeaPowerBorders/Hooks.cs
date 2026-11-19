using MapControl;

namespace SeaPowerBorders;

/// <summary>
/// Every game method the mod hooks (HookRegistry, in the shared skeleton):
/// - MainGameViewModel.OnStart / GameMain.OnDestroy - mission ready / mission unloading (MissionHooks), at load. Without them the
/// mod stays out of the way entirely.
/// - RenderedLayer.OnRender - runs every frame for the map control that shows the live map; it hands over that map so the labels
/// can be laid into it. Applied once NoesisGUI runs: patching it earlier would run Noesis dependency-property registration
/// (RenderedLayer's static constructor) before Noesis exists - a native crash on startup.
/// </summary>
internal static class Hooks
{
    public static void Declare()
    {
        MissionHooks.Declare();
        HookRegistry.Add("Map", HookStage.AfterNoesis, "no labels on the map")
            .Hook("MapControl.RenderedLayer", "OnRender", null, typeof(Hooks), postfix: nameof(MapRendered));
    }

    private static void MapRendered(RenderedLayer __instance) => (ModHost.Instance as Host)?.MapRendered(__instance);
}
