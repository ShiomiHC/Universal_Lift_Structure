namespace Universal_Lift_Structure;

[HarmonyPatch(typeof(PlaySettings), "DoMapControls")]
public static class Patch_PlaySettings_DoMapControls
{
    public static void Postfix(PlaySettings __instance, WidgetRow row)
    {
        UniversalLiftStructureSettings settings = UniversalLiftStructureMod.Settings;
        if (settings is null)
        {
            return;
        }


        bool enableBefore = settings.enableOverlayDisplay;
        bool enableValue = enableBefore;
        row.ToggleableIcon(
            ref enableValue,
            ULS_Textures.OverlayDisplayMasterToggle,
            "ULS_OverlayDisplayMasterToggleButton".Translate(),
            SoundDefOf.Mouseover_ButtonToggle);
        if (enableValue != enableBefore)
        {
            settings.enableOverlayDisplay = enableValue;
            settings.Write();
        }
    }
}