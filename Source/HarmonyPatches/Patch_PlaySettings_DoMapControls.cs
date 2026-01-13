namespace Universal_Lift_Structure;

// ============================================================
// 【Harmony 补丁：PlaySettings.DoMapControls】
// ============================================================
// 作用：在地图右下角的控制条中注入“显示/隐藏覆盖层”的开关。
// ============================================================
[HarmonyPatch(typeof(PlaySettings), "DoMapControls")]
public static class Patch_PlaySettings_DoMapControls
{
    // ============================================================
    // 【后置补丁】
    // ============================================================
    // 在绘制地图控制按钮时追加自定义按钮。
    // ============================================================
    public static void Postfix(PlaySettings __instance, WidgetRow row)
    {
        UniversalLiftStructureSettings settings = UniversalLiftStructureMod.Settings;
        if (settings is null)
        {
            return;
        }


        // 绑定 setting.enableOverlayDisplay 状态
        bool enableBefore = settings.enableOverlayDisplay;
        bool enableValue = enableBefore;

        // 绘制切换按钮
        row.ToggleableIcon(
            ref enableValue,
            ULS_Textures.OverlayDisplayMasterToggle,
            "ULS_OverlayDisplayMasterToggleButton".Translate(),
            SoundDefOf.Mouseover_ButtonToggle);

        // 如果状态改变，保存设置
        if (enableValue != enableBefore)
        {
            settings.enableOverlayDisplay = enableValue;
            settings.Write();
        }
    }
}