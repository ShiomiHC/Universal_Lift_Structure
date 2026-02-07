namespace Universal_Lift_Structure;

// ============================================================
// 【Harmony 补丁：Thing.Print - 隐藏降下的控制器】
// ============================================================
// 作用：当配置项 hideControllerWhenStored 开启时，
// 跳过降下且存储有物品的控制器的渲染，显露下方地面。
//
// 【判定条件】
// 1. 必须是 Building_WallController 类型
// 2. 配置项 hideControllerWhenStored 已开启
// 3. 控制器已存储物品（HasStored == true）
// 4. 控制器不在升降过程中（InLiftProcess == false）
//
// 【注意事项】
// - 仅隐藏建筑图形，选择框保持可见
// - 对自动控制器和手动控制器均生效
// ============================================================
[HarmonyPatch(typeof(Thing), nameof(Thing.Print))]
public static class Patch_Thing_Print_HideStoredController
{
    // ============================================================
    // 【前置补丁】
    // ============================================================
    // 在 Thing.Print() 执行前检查是否应跳过渲染。
    //
    // 【返回值】
    // - true: 继续执行原始 Print() 方法
    // - false: 跳过原始 Print() 方法（隐藏控制器）
    // ============================================================
    public static bool Prefix(Thing __instance)
    {
        // 快速类型检查：仅处理控制器
        if (__instance is not Building_WallController controller)
        {
            return true;
        }

        // 使用辅助类判断是否应该隐藏
        bool shouldHide = ULS_ControllerHideHelper.ShouldHideController(controller);

        // 如果应该隐藏，跳过渲染
        return !shouldHide;
    }
}
