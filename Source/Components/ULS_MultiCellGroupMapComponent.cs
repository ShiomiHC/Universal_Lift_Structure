namespace Universal_Lift_Structure;

public class ULS_MultiCellGroupMapComponent : MapComponent
{
    // 存储所有多格建筑的分组记录（会被序列化）
    internal List<ULS_MultiCellGroupRecord> groupRecords = new();

    // 当主控被飞船传送时，其他组员可能还未生成，延迟到下一 Tick 统一重建
    private HashSet<Building_WallController> pendingRebuildControllers = new HashSet<Building_WallController>();

    public void RegisterPendingRebuild(Building_WallController controller)
    {
        if (controller != null)
        {
            pendingRebuildControllers.Add(controller);
        }
    }

    // rootCell → GroupRecord 索引（不序列化，加载后重建）
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

    // 重建 groupByRootCell 索引，并向成员控制器单向派发多格组坐标（SSOT）
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

    // 此时所有 Thing 均已完成 SpawnSetup
    public override void FinalizeInit()
    {
        base.FinalizeInit();

        // 检查旧存档补足成员名单，并完成 SSOT 的组坐标运行时派发
        ULS_BackwardCompatibility.CheckAndFixLegacyGroups(this, map);
    }

    public override void MapComponentTick()
    {
        base.MapComponentTick();

        if (pendingRebuildControllers.Count > 0)
        {
            using var _ = new PooledList<Building_WallController>(out var toRebuild);
            toRebuild.AddRange(pendingRebuildControllers);
            pendingRebuildControllers.Clear();

            foreach (var controller in toRebuild)
            {
                if (controller != null && controller.Spawned && controller.HasStored)
                {
                    controller.TryRebuildMultiCellGroupAfterTransfer();
                }
            }
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

    public void DestroyAndRemoveGroup(IntVec3 rootCell)
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
                master.DestroyStored(mapInstance);
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

                    controller.DestroyStored(mapInstance);
                    break;
                }
            }
        }

        RemoveGroup(rootCell);
    }

    // SSOT 重构后主控不在 memberControllerCells 名单中，需单列清理
    private void ClearMemberControllerFlags(ULS_MultiCellGroupRecord record)
    {
        Map mapInstance = map;
        if (mapInstance is null || record == null)
        {
            return;
        }

        // 清理主控
        if (record.masterControllerCell.IsValid && ULS_Utility.TryGetControllerAt(mapInstance, record.masterControllerCell, out Building_WallController masterController))
        {
            masterController.MultiCellGroupRootCell = IntVec3.Invalid;
            masterController.ClearLiftProcessAndRemoveBlocker();
        }

        // 清理所有成员
        if (record.memberControllerCells != null)
        {
            foreach (var cell in record.memberControllerCells)
            {
                if (!ULS_Utility.TryGetControllerAt(mapInstance, cell, out Building_WallController controller))
                {
                    continue;
                }

                controller.MultiCellGroupRootCell = IntVec3.Invalid;
                controller.ClearLiftProcessAndRemoveBlocker();
            }
        }
    }
}
