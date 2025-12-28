namespace Universal_Lift_Structure;

public class ULS_MultiCellGroupMapComponent : MapComponent
{
    private List<ULS_MultiCellGroupRecord> groupRecords = new();


    private readonly Dictionary<IntVec3, ULS_MultiCellGroupRecord> groupByRootCell = new();

    public ULS_MultiCellGroupMapComponent(Map map) : base(map)
    {
    }

    public override void ExposeData()
    {
        base.ExposeData();
        Scribe_Collections.Look(ref groupRecords, "ulsMultiCellGroups", LookMode.Deep);

        if (Scribe.mode is LoadSaveMode.PostLoadInit)
        {
            RebuildIndex();
        }
    }

    private void RebuildIndex()
    {
        groupByRootCell.Clear();
        groupRecords ??= new();

        foreach (var record in groupRecords)
        {
            if (record is null || !record.rootCell.IsValid)
            {
                continue;
            }

            groupByRootCell[record.rootCell] = record;
        }
    }

    public bool HasGroup(IntVec3 rootCell)
    {
        return rootCell.IsValid && groupByRootCell.ContainsKey(rootCell);
    }

    public bool TryGetGroup(IntVec3 rootCell, out ULS_MultiCellGroupRecord record)
    {
        record = null;
        if (!rootCell.IsValid)
        {
            return false;
        }

        return groupByRootCell.TryGetValue(rootCell, out record);
    }

    public bool TryAddGroup(ULS_MultiCellGroupRecord record)
    {
        if (record is null || !record.rootCell.IsValid)
        {
            return false;
        }

        if (groupByRootCell.ContainsKey(record.rootCell))
        {
            return false;
        }

        groupRecords.Add(record);
        groupByRootCell.Add(record.rootCell, record);
        return true;
    }

    public void RemoveGroup(IntVec3 rootCell)
    {
        if (!TryGetGroup(rootCell, out ULS_MultiCellGroupRecord record))
        {
            return;
        }

        ClearMemberControllerFlags(record);
        groupByRootCell.Remove(rootCell);
        groupRecords.Remove(record);
    }

    public void RefundAndRemoveGroup(IntVec3 rootCell)
    {
        if (!TryGetGroup(rootCell, out ULS_MultiCellGroupRecord record))
        {
            return;
        }

        Map mapInstance = map;
        if (mapInstance is not null)
        {
            if (ULS_Utility.TryGetControllerAt(mapInstance, record.masterControllerCell,
                    out Building_WallController master))
            {
                master.RefundStored(mapInstance);
            }
            else
            {
                foreach (var t in record.memberControllerCells)
                {
                    if (!ULS_Utility.TryGetControllerAt(mapInstance, t,
                            out Building_WallController controller))
                    {
                        continue;
                    }

                    if (!controller.HasStored)
                    {
                        continue;
                    }

                    controller.RefundStored(mapInstance);
                    break;
                }
            }
        }

        RemoveGroup(rootCell);
    }

    private void ClearMemberControllerFlags(ULS_MultiCellGroupRecord record)
    {
        Map mapInstance = map;
        if (mapInstance is null || record?.memberControllerCells is null)
        {
            return;
        }

        foreach (var cell in record.memberControllerCells)
        {
            if (!ULS_Utility.TryGetControllerAt(mapInstance, cell, out Building_WallController controller))
            {
                continue;
            }

            controller.MultiCellGroupRootCell = IntVec3.Invalid;
        }
    }
}