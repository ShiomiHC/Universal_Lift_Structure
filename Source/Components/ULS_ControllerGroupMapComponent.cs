namespace Universal_Lift_Structure;

public class ULS_ControllerGroupMapComponent : MapComponent
{
    // 追踪当前正在进行升降动画的控制器，用于优化 Patch_DrawManager_DynamicThings 渲染
    private readonly HashSet<Building_WallController> activeAnimatingControllers =
        new HashSet<Building_WallController>();

    // 追踪地图上所有控制器，用于 Ghost 渲染遍历
    private readonly HashSet<Building_WallController> allControllers = new HashSet<Building_WallController>();

    public void RegisterAnimatingController(Building_WallController controller)
    {
        if (controller != null)
        {
            activeAnimatingControllers.Add(controller);
        }
    }

    public void DeregisterAnimatingController(Building_WallController controller)
    {
        if (controller != null)
        {
            activeAnimatingControllers.Remove(controller);
        }
    }

    public HashSet<Building_WallController> GetActiveAnimatingControllers()
    {
        return activeAnimatingControllers;
    }

    public HashSet<Building_WallController> GetAllControllers()
    {
        return allControllers;
    }

    // 双向映射，必须保持同步
    // GroupId → ControllerCells（一对多）
    private readonly Dictionary<int, List<IntVec3>> controllerCellsByGroupId = new();

    // ControllerCell → GroupId（一对一）
    private readonly Dictionary<IntVec3, int> groupIdByControllerCell = new();

    private bool indexBuilt;
    // 防止递归调用
    private bool rebuildInProgress;

    public ULS_ControllerGroupMapComponent(Map map) : base(map)
    {
    }

    // 索引数据不序列化，完全从控制器的 ControllerGroupId 字段重建
    public override void ExposeData()
    {
        base.ExposeData();

        if (Scribe.mode is LoadSaveMode.PostLoadInit)
        {
            indexBuilt = false;
        }
    }

    public override void FinalizeInit()
    {
        base.FinalizeInit();
        RebuildIndexFromMap();
        // 修复因保护模式存档导致 HP=-1 的建筑实例
        ULS_DefAdjuster.TryRestoreHpOnMap(map);
    }

    // 仅遍历 activeAnimatingControllers（通常数量极少），空闲控制器不消耗 CPU
    public override void MapComponentTick()
    {
        base.MapComponentTick();

        if (activeAnimatingControllers.Count == 0)
        {
            return;
        }

        // 使用临时列表避免在遍历时修改集合
        using var _ = new PooledList<Building_WallController>(out var toTick);
        foreach (var controller in activeAnimatingControllers)
        {
            if (controller != null && controller.Spawned)
            {
                toTick.Add(controller);
            }
        }

        foreach (var controller in toTick)
        {
            controller.TickLiftProcess();
        }
    }

    private void EnsureIndex()
    {
        if (indexBuilt || rebuildInProgress)
        {
            return;
        }

        RebuildIndexFromMap();
    }

    // 优先分配 1-1000 范围内的最小可用 ID；若全部占用则回退到 maxId+1
    public int CreateNewGroupId()
    {
        if (!indexBuilt && !rebuildInProgress)
        {
            EnsureIndex();
        }

        for (int candidate = 1; candidate <= 1000; candidate++)
        {
            if (!controllerCellsByGroupId.ContainsKey(candidate))
            {
                return candidate;
            }
        }

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

        allControllers.Add(controller);

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

        if (!groupIdByControllerCell.Remove(cell, out int groupId))
        {
            return;
        }

        if (controllerCellsByGroupId.TryGetValue(groupId, out List<IntVec3> list))
        {
            list.Remove(cell);
            if (list.Count == 0)
            {
                controllerCellsByGroupId.Remove(groupId);
            }
        }
    }

    public void DeregisterController(Building_WallController controller)
    {
        if (controller == null) return;

        allControllers.Remove(controller);
        RemoveControllerCell(controller.Position);
    }

    // 返回内部列表的直接引用，调用者不应修改
    public bool TryGetGroupControllerCells(int groupId, out List<IntVec3> cells)
    {
        EnsureIndex();
        return controllerCellsByGroupId.TryGetValue(groupId, out cells);
    }

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

        foreach (var cell in controllerCells)
        {
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

    // sourceCells 是内部列表引用，需先复制再调用 AssignControllerCellsToGroup，避免遍历中修改集合
    public void MergeGroups(int targetGroupId, int sourceGroupId)
    {
        if (targetGroupId < 1 || sourceGroupId < 1 || targetGroupId == sourceGroupId)
        {
            return;
        }

        EnsureIndex();
        if (!controllerCellsByGroupId.TryGetValue(sourceGroupId, out List<IntVec3> sourceCells) ||
            sourceCells.Count == 0)
        {
            return;
        }

        using var _ = new PooledList<IntVec3>(out var copy);
        copy.AddRange(sourceCells);
        AssignControllerCellsToGroup(copy, targetGroupId);
    }

    private void RebuildIndexFromMap()
    {
        rebuildInProgress = true;
        controllerCellsByGroupId.Clear();
        groupIdByControllerCell.Clear();
        allControllers.Clear();

        if (map is null)
        {
            rebuildInProgress = false;
            indexBuilt = true;
            return;
        }

        List<Thing> things = map.listerThings.ThingsInGroup(ThingRequestGroup.BuildingArtificial);
        foreach (var t in things)
        {
            if (t is not Building_WallController controller || controller.Destroyed)
            {
                continue;
            }

            int id = controller.ControllerGroupId;
            if (id < 1)
            {
                id = CreateNewGroupId();
                controller.ControllerGroupId = id;
            }

            RegisterOrUpdateController(controller);
        }

        rebuildInProgress = false;
        indexBuilt = true;
    }
}
