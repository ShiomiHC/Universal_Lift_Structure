namespace Universal_Lift_Structure;

public class ULS_ControllerGroupMapComponent : MapComponent
{
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

        if (Scribe.mode is LoadSaveMode.PostLoadInit)
        {
            indexBuilt = false;
        }
    }

    public override void FinalizeInit()
    {
        base.FinalizeInit();
        RebuildIndexFromMap();
    }

    private void EnsureIndex()
    {
        if (indexBuilt || rebuildInProgress)
        {
            return;
        }

        RebuildIndexFromMap();
    }

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