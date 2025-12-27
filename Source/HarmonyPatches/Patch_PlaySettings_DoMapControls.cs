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

        // 右下角 Overlay：叠加层显示总开关（子项在 Mod 设置菜单中配置）。
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
