namespace Universal_Lift_Structure;

// ============================================================
// 【Harmony 补丁：监听电力状态变化】
// ============================================================
// 拦截 CompPowerTrader.PowerOn 属性的 setter，监听电力状态变化
// 当控制器的电力状态改变时，立即失效 Gizmo 缓存，确保 UI 及时更新
// ============================================================

[HarmonyPatch(typeof(CompPowerTrader), nameof(CompPowerTrader.PowerOn), MethodType.Setter)]
public static class Patch_CompPowerTrader_PowerOn_Setter
{
    // ============================================================
    // 【后置拦截：CompPowerTrader.PowerOn setter】
    // ============================================================
    // 当任意建筑的电力状态改变后调用
    //
    // 【作用】
    // - 检测是否为控制器建筑
    // - 如果是控制器且电力状态发生变化，立即失效 Gizmo 缓存
    // - 确保 Gizmo 可以及时响应电力恢复/断电
    //
    // 【参数说明】
    // - __instance: 电力组件实例
    // - value: 新的电力状态值
    //
    // 【调用时机】
    // - 电力网络状态变化时
    // - 电力连接建立/断开时
    // ============================================================
    [HarmonyPostfix]
    public static void Postfix(CompPowerTrader __instance, bool value)
    {
        // 检查是否为控制器建筑
        if (__instance?.parent is Building_WallController controller)
        {
            // 电力状态变化时立即失效 Gizmo 缓存
            // 这样当玩家选中控制器时，Gizmo 会立即刷新并显示正确的状态
            controller.InvalidateGizmoCache();
        }
    }
}
