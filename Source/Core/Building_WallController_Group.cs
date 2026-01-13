namespace Universal_Lift_Structure;

public partial class Building_WallController
{
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


    private void GetMultiCellMemberControllersOrSelf(Map map, HashSet<Building_WallController> outResult)
    {
        outResult.Clear();
        outResult.Add(this);

        if (map == null)
        {
            return;
        }

        if (!multiCellGroupRootCell.IsValid)
        {
            return;
        }

        ULS_MultiCellGroupMapComponent multiCellComp = map.GetComponent<ULS_MultiCellGroupMapComponent>();

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
                result.Add(controller);
            }
        }

        return result;
    }
}
