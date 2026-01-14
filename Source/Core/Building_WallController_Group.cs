namespace Universal_Lift_Structure;

public partial class Building_WallController
{
    // ============================================================
    // 【获取当前选中的控制器】
    // ============================================================
    // 获取当前选中的所有墙体控制器
    //
    // 【返回值】
    // - 选中的控制器列表（不为空）
    // ============================================================
    // 缓存字段
    private static List<Building_WallController> cachedSelectedControllers;
    private static int cachedSelectionTick = -1;

    private static List<Building_WallController> GetSelectedControllers()
    {
        // 检查缓存是否有效
        if (cachedSelectionTick == Find.TickManager.TicksGame && cachedSelectedControllers != null)
        {
            return cachedSelectedControllers;
        }

        if (cachedSelectedControllers == null)
        {
            cachedSelectedControllers = new List<Building_WallController>();
        }
        else
        {
            cachedSelectedControllers.Clear();
        }

        cachedSelectionTick = Find.TickManager.TicksGame;

        if (Find.Selector == null)
        {
            return cachedSelectedControllers;
        }

        List<object> selectedObjects = Find.Selector.SelectedObjectsListForReading;
        if (selectedObjects == null)
        {
            return cachedSelectedControllers;
        }

        for (int i = 0; i < selectedObjects.Count; i++)
        {
            if (selectedObjects[i] is Building_WallController controller)
            {
                cachedSelectedControllers.Add(controller);
            }
        }

        return cachedSelectedControllers;
    }


    // ============================================================
    // 【获取多格组成员】
    // ============================================================
    // 获取多格结构的成员控制器（包括自身）
    //
    // 【参数说明】
    // - map: 地图
    // - outResult: 输出结果集合
    // ============================================================
    private void GetMultiCellMemberControllersOrSelf(Map map, HashSet<Building_WallController> outResult)
    {
        outResult.Clear();
        outResult.Add(this);

        if (map == null)
        {
            return;
        }

        // 如果不是多格结构的一部分，直接返回（已包含自身）
        if (!multiCellGroupRootCell.IsValid)
        {
            return;
        }

        ULS_MultiCellGroupMapComponent multiCellComp = map.GetComponent<ULS_MultiCellGroupMapComponent>();

        // 验证多格组组件和记录
        if (multiCellComp == null ||
            !multiCellComp.TryGetGroup(multiCellGroupRootCell, out var record) ||
            record == null ||
            record.memberControllerCells == null)
        {
            return;
        }

        foreach (var cell in record.memberControllerCells)
        {
            if (ULS_Utility.TryGetControllerAt(map, cell, out var controller))
            {
                outResult.Add(controller);
            }
        }
    }


    // ============================================================
    // 【检查选中是否包含多格隐藏组】
    // ============================================================
    // 检查选中的控制器中是否包含多格隐藏组的成员
    //
    // 【参数说明】
    // - selectedControllers: 选中的控制器列表
    //
    // 【返回值】
    // - true: 包含多格组成员
    // ============================================================
    private static bool AnySelectedControllerInMultiCellHiddenGroup(List<Building_WallController> selectedControllers)
    {
        if (selectedControllers == null || selectedControllers.Count <= 0)
        {
            return false;
        }

        foreach (var controller in selectedControllers)
        {
            if (controller is { MultiCellGroupRootCell.IsValid: true })
            {
                return true;
            }
        }

        return false;
    }


    // ============================================================
    // 【扩展选中列表】
    // ============================================================
    // 扩展选中的控制器列表，包含其所属的多格隐藏组的所有成员
    //
    // 【参数说明】
    // - map: 地图
    // - selectedControllers: 选中的控制器列表
    // - outResult: 输出扩展后的控制器列表（会被清空后填充）
    // ============================================================
    private static void ExpandSelectedControllersToMultiCellHiddenGroupMembers(
        Map map,
        List<Building_WallController> selectedControllers,
        List<Building_WallController> outResult)
    {
        outResult.Clear();

        if (map == null || selectedControllers == null || selectedControllers.Count <= 0)
        {
            return;
        }

        ULS_MultiCellGroupMapComponent multiCellComp = map.GetComponent<ULS_MultiCellGroupMapComponent>();

        using var _ = new PooledHashSet<IntVec3>(out var uniqueCells);

        foreach (var controller in selectedControllers)
        {
            if (controller == null || controller.Map != map || !controller.Spawned)
            {
                continue;
            }


            if (!controller.MultiCellGroupRootCell.IsValid || multiCellComp == null)
            {
                uniqueCells.Add(controller.Position);
                continue;
            }


            if (!multiCellComp.TryGetGroup(controller.MultiCellGroupRootCell, out var record) || record == null)
            {
                uniqueCells.Add(controller.Position);
                continue;
            }

            List<IntVec3> memberCells = record.memberControllerCells;
            if (memberCells == null || memberCells.Count <= 0)
            {
                uniqueCells.Add(controller.Position);
                continue;
            }

            foreach (var t in memberCells)
            {
                uniqueCells.Add(t);
            }
        }


        foreach (IntVec3 cell in uniqueCells)
        {
            // 尝试获取该位置的控制器并添加到结果中
            if (ULS_Utility.TryGetControllerAt(map, cell, out var controller))
            {
                outResult.Add(controller);
            }
        }
    }
}
