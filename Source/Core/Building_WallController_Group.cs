namespace Universal_Lift_Structure;

public partial class Building_WallController
{
    // 复用静态列表避免 GC 压力；不使用 tick 缓存，避免暂停时缓存失效
    private static readonly List<Building_WallController> selectedControllersList = new();

    private static List<Building_WallController> GetSelectedControllers()
    {
        selectedControllersList.Clear();

        if (Find.Selector == null)
        {
            return selectedControllersList;
        }

        List<object> selectedObjects = Find.Selector.SelectedObjectsListForReading;
        if (selectedObjects == null)
        {
            return selectedControllersList;
        }

        for (int i = 0; i < selectedObjects.Count; i++)
        {
            if (selectedObjects[i] is Building_WallController controller)
            {
                selectedControllersList.Add(controller);
            }
        }

        return selectedControllersList;
    }


    private void GetMultiCellMemberControllersOrSelf(Map map, HashSet<Building_WallController> outResult)
    {
        outResult.Clear();
        outResult.Add(this);

        if (map == null)
        {
            return;
        }

        // 如果不是多格结构的一部分，直接返回（已包含自身）
        if (!MultiCellGroupRootCell.IsValid)
        {
            return;
        }

        ULS_MultiCellGroupMapComponent multiCellComp = map.GetComponent<ULS_MultiCellGroupMapComponent>();

        if (multiCellComp == null ||
            !multiCellComp.TryGetGroup(MultiCellGroupRootCell, out var record) ||
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
            if (ULS_Utility.TryGetControllerAt(map, cell, out var controller))
            {
                outResult.Add(controller);
            }
        }
    }
}
