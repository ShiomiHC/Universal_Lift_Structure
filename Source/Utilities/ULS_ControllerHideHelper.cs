namespace Universal_Lift_Structure;

// ============================================================
// 【控制器隐藏辅助类】
// ============================================================
// 提供控制器隐藏功能的共用判断逻辑
//
// 【用途】
// - Harmony 补丁：判断是否应该隐藏控制器
// - 设置界面：判断哪些控制器需要刷新
// ============================================================
internal static class ULS_ControllerHideHelper
{
    // ============================================================
    // 【判断控制器是否应该被隐藏】
    // ============================================================
    // 根据配置和控制器状态判断是否应该隐藏
    //
    // 【参数说明】
    // - controller: 要判断的控制器
    // - settings: Mod 设置（可选，如果为 null 则自动获取）
    //
    // 【返回值】
    // - true: 应该隐藏
    // - false: 不应该隐藏
    // ============================================================
    internal static bool ShouldHideController(Building_WallController controller,
        UniversalLiftStructureSettings settings = null)
    {
        if (controller == null)
        {
            return false;
        }

        // 获取设置
        settings ??= UniversalLiftStructureMod.Settings;
        if (settings is not { hideControllerWhenStored: true })
        {
            return false;
        }

        // 检查是否有存储内容（复用方法）
        return HasStoredContent(controller);
    }

    // ============================================================
    // 【判断控制器是否有存储内容】
    // ============================================================
    // 检查控制器是否有存储内容（不考虑配置）
    //
    // 【参数说明】
    // - controller: 要判断的控制器
    //
    // 【返回值】
    // - true: 有存储内容
    // - false: 无存储内容
    // ============================================================
    internal static bool HasStoredContent(Building_WallController controller)
    {
        if (controller == null)
        {
            return false;
        }

        // 检查是否已存储物品（单格控制器）
        if (controller.HasStored)
        {
            return true;
        }

        // 升降过程中不隐藏（保持动画可见）
        if (controller.InLiftProcess)
        {
            return false;
        }

        // 检查是否为多格结构的成员
        if (controller.MultiCellGroupRootCell.IsValid)
        {
            Map map = controller.Map;
            if (map != null)
            {
                // 获取根格控制器
                if (ULS_Utility.TryGetControllerAt(map, controller.MultiCellGroupRootCell,
                        out Building_WallController rootController))
                {
                    // 如果根格控制器已存储物品且不在升降过程中，此成员应该被隐藏
                    return rootController.HasStored && !rootController.InLiftProcess;
                }
            }
        }

        return false;
    }
}
