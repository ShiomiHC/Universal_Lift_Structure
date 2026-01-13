namespace Universal_Lift_Structure;

// ============================================================
// 【Harmony 补丁：地热发电机兼容修复】
// ============================================================
// 作用：修复地热发电机在被收纳状态下的组件报错与错误排热逻辑。
// 虽然地热发电机通常可移动性较差，但如果被强制收纳（或 Mod 允许搬运），其 CompTick 会因为 Map 为空而报错。
// ============================================================
[HarmonyPatch]
public static class Patch_GeothermalGenerator_Fix
{
    // ============================================================
    // 【前置拦截：CompPowerPlantSteam.CompTick】
    // ============================================================
    // 防止在收纳状态下（Map 为 null）执行 Tick 逻辑导致 NRE。
    // ============================================================
    [HarmonyPatch(typeof(CompPowerPlantSteam), nameof(CompPowerPlantSteam.CompTick))]
    [HarmonyPrefix]
    public static bool CompPowerPlantSteam_CompTick_Prefix(CompPowerPlantSteam __instance)
    {
        // 如果父级建筑未生成或 Map 为空（处于收纳状态），跳过逻辑执行
        if (__instance.parent == null || !__instance.parent.Spawned || __instance.parent.Map == null)
        {
            return false;
        }

        return true;
    }


    // ============================================================
    // 【前置拦截：CompHeatPusher.ShouldPushHeatNow】
    // ============================================================
    // 防止在收纳状态下由于容器已生成而继续排热（因为 CompHeatPusher 可能只检查 parent != null）。
    // ============================================================
    [HarmonyPatch(typeof(CompHeatPusher), "get_ShouldPushHeatNow")]
    [HarmonyPrefix]
    public static bool CompHeatPusher_ShouldPushHeatNow_Prefix(CompHeatPusher __instance, ref bool __result)
    {
        // 如果父级建筑未生成（处于收纳状态），强制返回 false 停止排热
        if (__instance.parent == null || !__instance.parent.Spawned)
        {
            __result = false;
            return false;
        }

        return true;
    }
}
