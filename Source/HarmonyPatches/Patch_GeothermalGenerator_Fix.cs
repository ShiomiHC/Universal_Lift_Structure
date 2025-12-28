using HarmonyLib;
using RimWorld;
using Verse;

namespace Universal_Lift_Structure;

/// <summary>
/// 修复地热发电机在收纳状态下的组件报错与排热逻辑。
/// </summary>
[HarmonyPatch]
public static class Patch_GeothermalGenerator_Fix
{
    /// <summary>
    /// 拦截 CompPowerPlantSteam.CompTick，防止在收纳状态下因 Map 为空导致的报错。
    /// </summary>
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

    /// <summary>
    /// 拦截 CompHeatPusher.ShouldPushHeatNow，防止在收纳状态下由于容器已生成而继续排热。
    /// </summary>
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
