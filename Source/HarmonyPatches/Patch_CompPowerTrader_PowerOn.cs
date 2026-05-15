namespace Universal_Lift_Structure;

// ============================================================
// 【Harmony 补丁：监听电力状态变化】
// ============================================================
// 拦截 CompPowerTrader.PowerOn 属性的 setter，监听电力状态变化
// 当组内任意控制器的电力状态改变时，失效整组的 Gizmo 缓存
// 原因：RefreshGizmoCache 扫描整组判断电力，任何成员断/通电
// 都可能影响其他成员的按钮禁用状态（GroupPowerInsufficient）
// ============================================================

[HarmonyPatch(typeof(CompPowerTrader), nameof(CompPowerTrader.PowerOn), MethodType.Setter)]
public static class Patch_CompPowerTrader_PowerOn_Setter
{
    [HarmonyPostfix]
    public static void Postfix(CompPowerTrader __instance, bool value)
    {
        if (__instance?.parent is not Building_WallController controller) return;

        Map map = controller.Map;
        ULS_ControllerGroupMapComponent groupComp = map?.GetComponent<ULS_ControllerGroupMapComponent>();
        int groupId = controller.ControllerGroupId;

        // 失效整组缓存：RefreshGizmoCache 的计算范围覆盖整组，
        // 任何成员电力变化都可能影响其他成员的按钮状态
        if (groupComp != null && groupId > 0 &&
            groupComp.TryGetGroupControllerCells(groupId, out List<IntVec3> cells) &&
            cells != null)
        {
            foreach (IntVec3 cell in cells)
            {
                if (ULS_Utility.TryGetControllerAt(map, cell, out Building_WallController member))
                    member.InvalidateGizmoCache();
            }
        }
        else
        {
            controller.InvalidateGizmoCache();
        }
    }
}
