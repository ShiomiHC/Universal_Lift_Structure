namespace Universal_Lift_Structure;

// 修复：地热发电机被强制收纳时，CompTick 因 Map 为空而报错，且 CompHeatPusher 会在收纳态持续排热。
[HarmonyPatch]
public static class Patch_GeothermalGenerator_Fix
{
    [HarmonyPatch(typeof(CompPowerPlantSteam), nameof(CompPowerPlantSteam.CompTick))]
    [HarmonyPrefix]
    public static bool CompPowerPlantSteam_CompTick_Prefix(CompPowerPlantSteam __instance)
    {
        if (__instance.parent == null || !__instance.parent.Spawned || __instance.parent.Map == null)
        {
            return false;
        }

        return true;
    }


    // CompHeatPusher 原版只检查 parent != null，收纳态下容器已生成因此仍会排热，需要额外拦截。
    [HarmonyPatch(typeof(CompHeatPusher), "get_ShouldPushHeatNow")]
    [HarmonyPrefix]
    public static bool CompHeatPusher_ShouldPushHeatNow_Prefix(CompHeatPusher __instance, ref bool __result)
    {
        if (__instance.parent == null || !__instance.parent.Spawned)
        {
            __result = false;
            return false;
        }

        return true;
    }
}
