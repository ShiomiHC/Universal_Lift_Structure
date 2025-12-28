namespace Universal_Lift_Structure;

public class ULS_AutoGroupMapComponent : MapComponent
{
    private class AutoGroupRuntime
    {
        public int membershipHash;
        public List<IntVec3> scanCells;

        public int lastSeenTick = int.MinValue;
        public int nextToggleAllowedTick;
        public int nextCheckTick;
    }


    private Dictionary<int, ULS_AutoGroupType> filterTypeByGroupId = new();


    private readonly List<int> autoGroupIds = new();
    private int autoGroupIndex;
    private bool autoGroupsDirty = true;
    private int lastRefreshTick;

    private readonly Dictionary<int, AutoGroupRuntime> runtimeByGroupId = new();

    public ULS_AutoGroupMapComponent(Map map) : base(map)
    {
    }

    public override void ExposeData()
    {
        base.ExposeData();

        List<int> keys = null;
        List<ULS_AutoGroupType> values = null;
        Scribe_Collections.Look(ref filterTypeByGroupId, "filterTypeByGroupId", LookMode.Value, LookMode.Value,
            ref keys, ref values);

        if (Scribe.mode is LoadSaveMode.PostLoadInit && filterTypeByGroupId is null)
        {
            filterTypeByGroupId = new();
        }
    }


    public ULS_AutoGroupType GetOrInitGroupFilterType(int groupId, ULS_AutoGroupType defaultType)
    {
        if (groupId < 1)
        {
            return defaultType;
        }

        filterTypeByGroupId ??= new();

        if (!filterTypeByGroupId.TryGetValue(groupId, out ULS_AutoGroupType t))
        {
            t = defaultType;
            filterTypeByGroupId[groupId] = t;
        }

        return t;
    }

    public bool TryGetGroupFilterType(int groupId, out ULS_AutoGroupType type)
    {
        type = default;
        if (groupId < 1 || filterTypeByGroupId is null)
        {
            return false;
        }

        return filterTypeByGroupId.TryGetValue(groupId, out type);
    }

    public void SetGroupFilterType(int groupId, ULS_AutoGroupType type)
    {
        if (groupId < 1)
        {
            return;
        }

        filterTypeByGroupId ??= new();
        filterTypeByGroupId[groupId] = type;
    }


    public void NotifyAutoGroupsDirty()
    {
        autoGroupsDirty = true;
    }

    public override void MapComponentTick()
    {
        base.MapComponentTick();

        if (map == null)
        {
            return;
        }

        int tick = Find.TickManager.TicksGame;


        if (autoGroupsDirty || tick - lastRefreshTick >= 250)
        {
            RefreshAutoGroups(tick);
        }

        if (autoGroupIds.Count == 0)
        {
            return;
        }


        int groupsPerTick = (autoGroupIds.Count + 29) / 30;
        if (groupsPerTick < 1)
        {
            groupsPerTick = 1;
        }

        for (int i = 0; i < groupsPerTick; i++)
        {
            if (autoGroupIds.Count == 0)
            {
                break;
            }

            if (autoGroupIndex >= autoGroupIds.Count)
            {
                autoGroupIndex = 0;
            }

            int groupId = autoGroupIds[autoGroupIndex++];
            TryProcessAutoGroup(groupId, tick);
        }
    }

    private void RefreshAutoGroups(int tick)
    {
        autoGroupsDirty = false;
        lastRefreshTick = tick;
        autoGroupIds.Clear();

        ULS_ControllerGroupMapComponent groupComp = map.GetComponent<ULS_ControllerGroupMapComponent>();
        if (groupComp == null)
        {
            return;
        }

        List<int> allGroupIds = new List<int>();
        groupComp.GetAllGroupIds(allGroupIds);


        HashSet<int> aliveAutoGroups = new HashSet<int>();

        foreach (var groupId in allGroupIds)
        {
            if (groupId < 1)
            {
                continue;
            }

            if (!TryGetGroupMarker(groupId, out ULS_AutoGroupMarker marker, out List<IntVec3> groupCells,
                    out string error))
            {
                if (error != null)
                {
                    Log.Error($"[ULS] AutoGroup invalid: groupId={groupId} error={error}");
                }

                continue;
            }

            if (marker == null)
            {
                continue;
            }

            autoGroupIds.Add(groupId);
            aliveAutoGroups.Add(groupId);

            if (!runtimeByGroupId.ContainsKey(groupId))
            {
                int phase = groupId % 30;
                runtimeByGroupId.Add(groupId, new AutoGroupRuntime { nextCheckTick = tick + phase });
            }


            if (groupCells is { Count: > 0 } &&
                ULS_Utility.TryGetControllerAt(map, groupCells[0], out Building_WallController c) && c != null)
            {
            }
        }


        List<int> toRemove = null;
        foreach (var kv in runtimeByGroupId)
        {
            if (!aliveAutoGroups.Contains(kv.Key))
            {
                toRemove ??= new List<int>();
                toRemove.Add(kv.Key);
            }
        }

        if (toRemove != null)
        {
            for (int i = 0; i < toRemove.Count; i++)
            {
                runtimeByGroupId.Remove(toRemove[i]);
            }
        }


        if (filterTypeByGroupId != null)
        {
            List<int> filterToRemove = null;
            foreach (var kv in filterTypeByGroupId)
            {
                if (!aliveAutoGroups.Contains(kv.Key))
                {
                    filterToRemove ??= new List<int>();
                    filterToRemove.Add(kv.Key);
                }
            }

            if (filterToRemove != null)
            {
                for (int i = 0; i < filterToRemove.Count; i++)
                {
                    filterTypeByGroupId.Remove(filterToRemove[i]);
                }
            }
        }


        if (autoGroupIndex >= autoGroupIds.Count)
        {
            autoGroupIndex = 0;
        }
    }

    private bool TryGetGroupMarker(int groupId, out ULS_AutoGroupMarker marker, out List<IntVec3> groupCells,
        out string error)
    {
        marker = null;
        groupCells = null;
        error = null;

        ULS_ControllerGroupMapComponent groupComp = map.GetComponent<ULS_ControllerGroupMapComponent>();
        if (groupComp == null)
        {
            return false;
        }

        if (!groupComp.TryGetGroupControllerCells(groupId, out groupCells) || groupCells == null ||
            groupCells.Count == 0)
        {
            return false;
        }


        Building_WallController representative = null;
        for (int i = 0; i < groupCells.Count; i++)
        {
            if (ULS_Utility.TryGetControllerAt(map, groupCells[i], out Building_WallController c) && c != null)
            {
                representative = c;
                break;
            }
        }

        if (representative == null)
        {
            return false;
        }

        marker = representative.GetComp<ULS_AutoGroupMarker>();
        if (marker == null)
        {
            return true;
        }


        for (int i = 0; i < groupCells.Count; i++)
        {
            if (!ULS_Utility.TryGetControllerAt(map, groupCells[i], out Building_WallController c) || c == null)
            {
                continue;
            }

            if (c.GetComp<ULS_AutoGroupMarker>() == null)
            {
                error = "group contains manual controller";
                return false;
            }
        }

        return true;
    }

    private void TryProcessAutoGroup(int groupId, int tick)
    {
        if (!runtimeByGroupId.TryGetValue(groupId, out AutoGroupRuntime runtime))
        {
            return;
        }

        if (tick < runtime.nextCheckTick)
        {
            return;
        }

        if (!TryGetGroupMarker(groupId, out ULS_AutoGroupMarker marker, out List<IntVec3> groupCells, out string error))
        {
            if (error != null)
            {
                Log.Error($"[ULS] AutoGroup invalid during tick: groupId={groupId} error={error}");
            }

            return;
        }

        if (marker == null)
        {
            return;
        }

        CompProperties_ULS_AutoGroupMarker props = marker.Props;
        int interval = props.checkIntervalTicks;
        if (interval < 30) interval = 30;
        runtime.nextCheckTick = tick + interval;


        ULS_AutoGroupType filterType = GetOrInitGroupFilterType(groupId, props.autoGroupType);


        int membershipHash = ComputeMembershipHash(groupCells);
        if (runtime.scanCells == null || runtime.scanCells.Count == 0 || runtime.membershipHash != membershipHash)
        {
            runtime.membershipHash = membershipHash;
            runtime.scanCells = BuildScanCells(groupCells, props.maxRadius);
        }


        bool hasTarget = false;
        for (int i = 0; i < runtime.scanCells.Count; i++)
        {
            IntVec3 cell = runtime.scanCells[i];
            if (!cell.InBounds(map))
            {
                continue;
            }

            List<Thing> things = map.thingGrid.ThingsListAtFast(cell);
            for (int j = 0; j < things.Count; j++)
            {
                if (things[j] is Pawn pawn && ULS_AutoGroupUtility.PawnMatchesGroupType(pawn, filterType))
                {
                    hasTarget = true;
                    break;
                }
            }

            if (hasTarget)
            {
                break;
            }
        }

        if (hasTarget)
        {
            runtime.lastSeenTick = tick;
        }


        Building_WallController controller = null;
        IntVec3 controllerCell = IntVec3.Invalid;
        bool groupHasAnyStored = false;
        bool groupHasAnyNotStored = false;
        for (int i = 0; i < groupCells.Count; i++)
        {
            if (!ULS_Utility.TryGetControllerAt(map, groupCells[i], out Building_WallController c) || c == null)
            {
                continue;
            }

            if (controller == null)
            {
                controller = c;
                controllerCell = groupCells[i];
            }

            if (c.HasStored)
            {
                groupHasAnyStored = true;
            }
            else
            {
                groupHasAnyNotStored = true;
            }

            if (groupHasAnyStored && groupHasAnyNotStored)
            {
                break;
            }
        }

        if (controller == null)
        {
            return;
        }


        bool closeWanted = hasTarget;
        if (!closeWanted && runtime.lastSeenTick != int.MinValue)
        {
            closeWanted = tick - runtime.lastSeenTick < props.closeDelayTicks;
        }

        if (tick < runtime.nextToggleAllowedTick)
        {
            return;
        }


        if (closeWanted)
        {
            if (groupHasAnyNotStored)
            {
                controller.AutoLowerGroup(controllerCell);
                runtime.nextToggleAllowedTick = tick + props.toggleCooldownTicks;
            }
        }
        else
        {
            if (groupHasAnyStored)
            {
                if (controller.AutoRaiseGroup())
                {
                    runtime.nextToggleAllowedTick = tick + props.toggleCooldownTicks;
                }
            }
        }
    }

    private static int ComputeMembershipHash(List<IntVec3> cells)
    {
        unchecked
        {
            int h = 17;
            if (cells != null)
            {
                for (int i = 0; i < cells.Count; i++)
                {
                    IntVec3 c = cells[i];
                    h = h * 31 + c.x;
                    h = h * 31 + c.z;
                }
            }

            return h;
        }
    }

    private List<IntVec3> BuildScanCells(List<IntVec3> groupCells, int maxRadius)
    {
        if (maxRadius < 0) maxRadius = 0;

        int estimated = groupCells != null ? groupCells.Count * (2 * maxRadius + 1) * (2 * maxRadius + 1) : 0;
        HashSet<IntVec3> set = estimated > 0 ? new HashSet<IntVec3>(estimated) : new HashSet<IntVec3>();
        if (groupCells != null)
        {
            for (int i = 0; i < groupCells.Count; i++)
            {
                IntVec3 center = groupCells[i];
                for (int dx = -maxRadius; dx <= maxRadius; dx++)
                {
                    for (int dz = -maxRadius; dz <= maxRadius; dz++)
                    {
                        IntVec3 cell = new IntVec3(center.x + dx, 0, center.z + dz);
                        if (cell.InBounds(map))
                        {
                            set.Add(cell);
                        }
                    }
                }
            }
        }

        return new List<IntVec3>(set);
    }
}