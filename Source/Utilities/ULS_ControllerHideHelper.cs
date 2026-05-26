namespace Universal_Lift_Structure;

internal static class ULS_ControllerHideHelper
{
    internal static bool ShouldHideController(Building_WallController controller,
        UniversalLiftStructureSettings settings = null)
    {
        if (controller == null)
        {
            return false;
        }

        bool hasStored = HasStoredContent(controller);

        // 桥版控制器在"桥撤除态"（HasStored && 非升降中）必须隐藏贴图，
        // 让下方恢复的水面对玩家可见；不受 hideControllerWhenStored 设置项约束。
        if (hasStored && controller.HasComp<Comp_ULS_Bridge>())
        {
            return true;
        }

        settings ??= UniversalLiftStructureMod.Settings;
        if (settings is not { hideControllerWhenStored: true })
        {
            return false;
        }

        return hasStored;
    }

    internal static bool HasStoredContent(Building_WallController controller)
    {
        if (controller == null)
        {
            return false;
        }

        if (controller.HasStored)
        {
            return true;
        }

        // 升降动画期间不隐藏，保持动画可见
        if (controller.InLiftProcess)
        {
            return false;
        }

        // 多格结构成员：检查根格控制器是否已存储且不在升降中
        if (controller.MultiCellGroupRootCell.IsValid)
        {
            Map map = controller.Map;
            if (map != null)
            {
                if (ULS_Utility.TryGetControllerAt(map, controller.MultiCellGroupRootCell,
                        out Building_WallController rootController))
                {
                    return rootController.HasStored && !rootController.InLiftProcess;
                }
            }
        }

        return false;
    }
}
