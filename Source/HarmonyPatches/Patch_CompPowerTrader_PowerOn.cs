namespace Universal_Lift_Structure;

// 拦截 CompPowerTrader.PowerOn setter，失效整组 Gizmo 缓存。
// 原因：RefreshGizmoCache 扫描整组判断电力，任何成员断/通电都可能影响其他成员的按钮禁用状态（GroupPowerInsufficient）。

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
