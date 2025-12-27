namespace Universal_Lift_Structure;

/// 文件意图：Building_WallController - 分组管理逻辑。
/// 包含：分组查询方法、分组扩展方法、分组验证方法、多格隐组处理。
public partial class Building_WallController
{
    // ==================== 分组查询方法 ====================

    /// 获取当前选中的所有控制器
    private static List<Building_WallController> GetSelectedControllers()
    {
        List<Building_WallController> result = new List<Building_WallController>();

        if (Find.Selector == null)
        {
            return result;
        }

        List<object> selectedObjects = Find.Selector.SelectedObjectsListForReading;
        if (selectedObjects == null)
        {
            return result;
        }

        for (int i = 0; i < selectedObjects.Count; i++)
        {
            if (selectedObjects[i] is Building_WallController controller)
            {
                result.Add(controller);
            }
        }

        return result;
    }

    /// 获取多格隐组成员控制器（或返回自身）
    private HashSet<Building_WallController> GetMultiCellMemberControllersOrSelf(Map map)
    {
        HashSet<Building_WallController> result = new HashSet<Building_WallController>();
        result.Add(this);

        if (map == null)
        {
            return result;
        }

        if (!multiCellGroupRootCell.IsValid)
        {
            return result;
        }

        ULS_MultiCellGroupMapComponent multiCellComp = map.GetComponent<ULS_MultiCellGroupMapComponent>();
        if (multiCellComp == null ||
            !multiCellComp.TryGetGroup(multiCellGroupRootCell, out var record) ||
            record == null ||
            record.memberControllerCells == null)
        {
            return result;
        }

        for (int i = 0; i < record.memberControllerCells.Count; i++)
        {
            IntVec3 cell = record.memberControllerCells[i];
            if (ULS_Utility.TryGetControllerAt(map, cell, out var controller) && controller != null)
            {
                result.Add(controller);
            }
        }

        return result;
    }

    // ==================== 分组扩展方法 ====================

    /// 检查选中的控制器中是否有任何一个属于多格隐组
    private static bool AnySelectedControllerInMultiCellHiddenGroup(List<Building_WallController> selectedControllers)
    {
        if (selectedControllers == null || selectedControllers.Count <= 0)
        {
            return false;
        }

        for (int i = 0; i < selectedControllers.Count; i++)
        {
            Building_WallController controller = selectedControllers[i];
            if (controller != null && controller.MultiCellGroupRootCell.IsValid)
            {
                return true;
            }
        }

        return false;
    }

    /// 将选中的控制器扩展到多格隐组成员
    private static List<Building_WallController> ExpandSelectedControllersToMultiCellHiddenGroupMembers(
        Map map,
        List<Building_WallController> selectedControllers)
    {
        List<Building_WallController> result = new List<Building_WallController>();

        if (map == null || selectedControllers == null || selectedControllers.Count <= 0)
        {
            return result;
        }

        ULS_MultiCellGroupMapComponent multiCellComp = map.GetComponent<ULS_MultiCellGroupMapComponent>();
        HashSet<IntVec3> uniqueCells = new HashSet<IntVec3>();

        // 收集所有相关的控制器格子
        for (int i = 0; i < selectedControllers.Count; i++)
        {
            Building_WallController controller = selectedControllers[i];
            if (controller == null || controller.Map != map || !controller.Spawned)
            {
                continue;
            }

            // 不在多格隐组中：直接添加
            if (!controller.MultiCellGroupRootCell.IsValid || multiCellComp == null)
            {
                uniqueCells.Add(controller.Position);
                continue;
            }

            // 在多格隐组中：添加所有成员
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

            for (int j = 0; j < memberCells.Count; j++)
            {
                uniqueCells.Add(memberCells[j]);
            }
        }

        // 根据格子获取控制器
        foreach (IntVec3 cell in uniqueCells)
        {
            if (ULS_Utility.TryGetControllerAt(map, cell, out var controller) && controller != null)
            {
                result.Add(controller);
            }
        }

        return result;
    }
}
