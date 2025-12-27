namespace Universal_Lift_Structure;

/// 文件意图：显式分组（玩家可见 GroupId）的地图级注册表。
/// - GroupId 为唯一权威，用于“组升起/组降下”的成员枚举。
/// - 索引不直接存档：每个控制器自身持久化 GroupId，本组件在加载后重建映射。
/// - 注意：groupMaxSize 不限制组的规模，仅在执行升起/降下时作为“可操作阈值”。
public class ULS_ControllerGroupMapComponent : MapComponent
{
    // 运行时索引：不直接存档，PostLoadInit/FinalizeInit 时重建。
    private readonly Dictionary<int, List<IntVec3>> controllerCellsByGroupId = new();
    private readonly Dictionary<IntVec3, int> groupIdByControllerCell = new();

    private bool indexBuilt;
    private bool rebuildInProgress;

    public ULS_ControllerGroupMapComponent(Map map) : base(map)
    {
    }

    public override void ExposeData()
    {
        base.ExposeData();
        // 方案C：不再持久化 nextGroupId，改为"最小未占用ID"算法。
        if (Scribe.mode is LoadSaveMode.PostLoadInit)
        {
            // 读档阶段控制器可能尚未完全 Spawn；在 FinalizeInit 再做一次重建。
            indexBuilt = false;
        }
    }

    public override void FinalizeInit()
    {
        base.FinalizeInit();
        RebuildIndexFromMap();
    }

    public void EnsureIndex()
    {
        if (indexBuilt || rebuildInProgress)
        {
            return;
        }

        RebuildIndexFromMap();
    }

    public int CreateNewGroupId()
    {
        // 方案C：最小未占用正整数ID（1..1000）。
        // 安全的索引确保：仅当索引未建且不在重建中时才触发重建。
        if (!indexBuilt && !rebuildInProgress)
        {
            EnsureIndex();
        }

        // 从 1 开始查找最小未占用ID。
        for (int candidate = 1; candidate <= 1000; candidate++)
        {
            if (!controllerCellsByGroupId.ContainsKey(candidate))
            {
                return candidate;
            }
        }

        // 1..1000 全被占用：回退策略（Log.Error + 返回 maxExistingGroupId + 1）。
        Log.Error("[ULS] CreateNewGroupId: 1..1000 全被占用，回退到 maxExistingGroupId + 1。");
        int maxExistingGroupId = 0;
        foreach (int existingId in controllerCellsByGroupId.Keys)
        {
            if (existingId > maxExistingGroupId)
            {
                maxExistingGroupId = existingId;
            }
        }

        return maxExistingGroupId + 1;
    }

    public void RegisterOrUpdateController(Building_WallController controller)
    {
        if (controller is null || controller.Map != map)
        {
            return;
        }

        IntVec3 cell = controller.Position;
        if (!cell.IsValid)
        {
            return;
        }

        int groupId = controller.ControllerGroupId;
        if (groupId < 1)
        {
            groupId = CreateNewGroupId();
            controller.ControllerGroupId = groupId;
        }

        // 从旧组移除
        if (groupIdByControllerCell.TryGetValue(cell, out int oldGroupId) && oldGroupId != groupId)
        {
            if (controllerCellsByGroupId.TryGetValue(oldGroupId, out List<IntVec3> oldList))
            {
                oldList.Remove(cell);
                if (oldList.Count == 0)
                {
                    controllerCellsByGroupId.Remove(oldGroupId);
                }
            }
        }

        groupIdByControllerCell[cell] = groupId;
        if (!controllerCellsByGroupId.TryGetValue(groupId, out List<IntVec3> list))
        {
            list = new();
            controllerCellsByGroupId.Add(groupId, list);
        }

        if (!list.Contains(cell))
        {
            list.Add(cell);
        }
    }

    public void RemoveControllerCell(IntVec3 cell)
    {
        if (!cell.IsValid)
        {
            return;
        }

        if (!groupIdByControllerCell.TryGetValue(cell, out int groupId))
        {
            return;
        }

        groupIdByControllerCell.Remove(cell);
        if (controllerCellsByGroupId.TryGetValue(groupId, out List<IntVec3> list))
        {
            list.Remove(cell);
            if (list.Count == 0)
            {
                controllerCellsByGroupId.Remove(groupId);
            }
        }
    }

    public bool TryGetGroupControllerCells(int groupId, out List<IntVec3> cells)
    {
        EnsureIndex();
        return controllerCellsByGroupId.TryGetValue(groupId, out cells);
    }

    
    /// 方法意图：枚举当前地图内存在的全部 GroupId。
    /// 说明：返回为“快照”；调用方可安全遍历，不应修改组件内部字典。
    
    public void GetAllGroupIds(List<int> outGroupIds)
    {
        if (outGroupIds is null)
        {
            return;
        }

        EnsureIndex();
        outGroupIds.Clear();
        foreach (int id in controllerCellsByGroupId.Keys)
        {
            outGroupIds.Add(id);
        }
    }

    public int GetGroupIdAtCell(IntVec3 cell)
    {
        EnsureIndex();
        return groupIdByControllerCell.TryGetValue(cell, out int id) ? id : 0;
    }

    public void AssignControllerCellsToGroup(List<IntVec3> controllerCells, int groupId)
    {
        if (controllerCells is not { Count: > 0 })
        {
            return;
        }

        if (groupId < 1)
        {
            groupId = CreateNewGroupId();
        }

        for (int i = 0; i < controllerCells.Count; i++)
        {
            IntVec3 cell = controllerCells[i];
            if (!cell.IsValid || !cell.InBounds(map))
            {
                continue;
            }

            if (!ULS_Utility.TryGetControllerAt(map, cell, out Building_WallController controller))
            {
                continue;
            }

            controller.ControllerGroupId = groupId;
            RegisterOrUpdateController(controller);
        }
    }

    public void MergeGroups(int targetGroupId, int sourceGroupId)
    {
        if (targetGroupId < 1 || sourceGroupId < 1 || targetGroupId == sourceGroupId)
        {
            return;
        }

        EnsureIndex();
        if (!controllerCellsByGroupId.TryGetValue(sourceGroupId, out List<IntVec3> sourceCells) || sourceCells.Count == 0)
        {
            return;
        }

        // 复制列表，避免边遍历边修改。
        List<IntVec3> copy = new(sourceCells);
        AssignControllerCellsToGroup(copy, targetGroupId);

        // AssignControllerCellsToGroup 会在 RegisterOrUpdateController 中把旧组清空并移除。
    }

    private void RebuildIndexFromMap()
    {
        rebuildInProgress = true;
        controllerCellsByGroupId.Clear();
        groupIdByControllerCell.Clear();

        if (map is null)
        {
            rebuildInProgress = false;
            indexBuilt = true;
            return;
        }

        List<Thing> things = map.listerThings.ThingsInGroup(ThingRequestGroup.BuildingArtificial);
        for (int i = 0; i < things.Count; i++)
        {
            if (things[i] is not Building_WallController controller || controller.Destroyed)
            {
                continue;
            }

            int id = controller.ControllerGroupId;
            if (id < 1)
            {
                // 旧档/异常：给一个新组，确保“控制器至少属于一个组”。
                id = CreateNewGroupId();
                controller.ControllerGroupId = id;
            }

            RegisterOrUpdateController(controller);
        }

        rebuildInProgress = false;
        indexBuilt = true;
    }
}
