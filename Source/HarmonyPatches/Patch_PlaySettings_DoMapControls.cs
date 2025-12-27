namespace Universal_Lift_Structure;

/// 文件意图：UI 补丁。在右下角 PlaySettings/Overlay 区域追加一个紫色图标按钮，用于全局切换虚影显示的开关。
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

        bool before = settings.showStoredGhostOverlay;
        bool value = before;

        row.ToggleableIcon(
            ref value,
            ULS_Textures.ShowStoredGhostOverlay,
            "ULS_ShowStoredGhostOverlayToggleButton".Translate(),
            SoundDefOf.Mouseover_ButtonToggle);

        if (value == before)
        {
            return;
        }

        settings.showStoredGhostOverlay = value;
        settings.Write();
    }
}
