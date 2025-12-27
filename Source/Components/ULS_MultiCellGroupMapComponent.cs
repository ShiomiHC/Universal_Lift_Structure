namespace Universal_Lift_Structure;

/// 文件意图：多格隐组持久化 —— MapComponent：地图级注册表，维护 rootCell 到 ULS_MultiCellGroupRecord 的映射。
/// 提供隐组的创建、查询、销毁与读档后成员状态重建。
public class ULS_MultiCellGroupMapComponent : MapComponent
{
    private List<ULS_MultiCellGroupRecord> groupRecords = new();

    // 运行时索引：不直接存档，PostLoadInit 时由 groupRecords 重建。
    private Dictionary<IntVec3, ULS_MultiCellGroupRecord> groupByRootCell = new();

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

        for (int i = 0; i < groupRecords.Count; i++)
        {
            ULS_MultiCellGroupRecord record = groupRecords[i];
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

        Map map = this.map;
        if (map is not null)
        {
            // 正常情况下 masterControllerCell 就是根格控制器。
            if (ULS_Utility.TryGetControllerAt(map, record.masterControllerCell, out Building_WallController master))
            {
                master.RefundStored(map);
            }
            else
            {
                // 数据异常时，尝试在成员中兜底找到实际存货者（不做 try/catch；找不到就直接跳过）。
                for (int i = 0; i < record.memberControllerCells.Count; i++)
                {
                    if (!ULS_Utility.TryGetControllerAt(map, record.memberControllerCells[i], out Building_WallController controller))
                    {
                        continue;
                    }

                    if (!controller.HasStored)
                    {
                        continue;
                    }

                    controller.RefundStored(map);
                    break;
                }
            }
        }

        RemoveGroup(rootCell);
    }

    private void ClearMemberControllerFlags(ULS_MultiCellGroupRecord record)
    {
        Map map = this.map;
        if (map is null || record?.memberControllerCells is null)
        {
            return;
        }

        for (int i = 0; i < record.memberControllerCells.Count; i++)
        {
            IntVec3 cell = record.memberControllerCells[i];
            if (!ULS_Utility.TryGetControllerAt(map, cell, out Building_WallController controller))
            {
                continue;
            }

            controller.MultiCellGroupRootCell = IntVec3.Invalid;
        }
    }
}
