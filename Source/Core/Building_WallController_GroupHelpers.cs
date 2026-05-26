namespace Universal_Lift_Structure;

public partial class Building_WallController
{
    private bool TryGetValidGroupCells(
        Map map,
        int groupId,
        int maxSize,
        bool showMessage,
        string emptyGroupMessageKey,
        out List<IntVec3> cells)
    {
        cells = null;
        ULS_ControllerGroupMapComponent groupComp = map.GetComponent<ULS_ControllerGroupMapComponent>();

        if (groupComp == null ||
            groupId < 1 ||
            !groupComp.TryGetGroupControllerCells(groupId, out cells) ||
            cells == null ||
            cells.Count == 0)
        {
            if (showMessage && !string.IsNullOrEmpty(emptyGroupMessageKey))
            {
                MessageReject(emptyGroupMessageKey, this);
            }

            return false;
        }

        if (cells.Count > maxSize)
        {
            if (showMessage)
            {
                MessageReject("ULS_GroupTooLarge", this, maxSize);
            }

            return false;
        }

        return true;
    }

    private void BuildUniqueRootCells(
        Map map,
        List<IntVec3> cells,
        List<IntVec3> uniqueRootCells,
        HashSet<IntVec3> seenRoots)
    {
        foreach (var cell in cells)
        {
            if (ULS_Utility.TryGetControllerAt(map, cell, out var controller))
            {
                // 多格组成员使用根位置去重
                IntVec3 rootCell = controller.MultiCellGroupRootCell.IsValid
                    ? controller.MultiCellGroupRootCell
                    : controller.Position;

                if (seenRoots.Add(rootCell))
                {
                    uniqueRootCells.Add(rootCell);
                }
            }
            else if (seenRoots.Add(cell))
            {
                uniqueRootCells.Add(cell);
            }
        }
    }

    private bool CheckGroupPowerReady(
        Map map,
        List<IntVec3> uniqueRootCells,
        bool showMessage)
    {
        if (!PowerFeatureEnabled)
        {
            return true;
        }

        foreach (var t in uniqueRootCells)
        {
            if (ULS_Utility.TryGetControllerAt(map, t, out var controller) &&
                !controller.IsReadyForLiftPower())
            {
                if (showMessage)
                {
                    MessageReject("ULS_GroupPowerInsufficient", controller);
                }

                return false;
            }
        }

        return true;
    }
}
