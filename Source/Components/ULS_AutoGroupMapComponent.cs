namespace Universal_Lift_Structure;

public class ULS_AutoGroupMapComponent : MapComponent
{
    private class AutoGroupRuntime
    {
        // 分组成员哈希值，变化时触发扫描区域重建
        public int membershipHash;

        public List<IntVec3> scanCells;

        // 启发式策略时使用：判断 Pawn 位置是否在扫描区域内
        public HashSet<IntVec3> scanCellsSet;

        // 上次检测到符合条件的 Pawn 的 Tick，用于延迟关闭逻辑
        public int lastSeenTick = int.MinValue;

        // 冷却机制：防止频繁切换
        public int nextToggleAllowedTick;

        public int nextCheckTick;
    }


    // 存储每个分组的 Pawn 过滤器类型（会被序列化）
    private Dictionary<int, ULS_AutoGroupType> filterTypeByGroupId = new();

    // 存储每个分组是否启用反转模式（会被序列化）
    // 正常模式：检测到目标 Pawn 时降下（让路），无目标时升起（阻挡）
    // 反转模式：检测到目标 Pawn 时升起（阻挡），无目标时降下（让路）
    private Dictionary<int, bool> invertedModeByGroupId = new();

    private readonly List<int> autoGroupIds = new();
    private int autoGroupIndex;
    private bool autoGroupsDirty = true;
    private int lastRefreshTick;

    private readonly Dictionary<int, AutoGroupRuntime> runtimeByGroupId = new();

    private ULS_ControllerGroupMapComponent cachedGroupComp;

    private ULS_ControllerGroupMapComponent GroupComp
    {
        get
        {
            if (cachedGroupComp == null && map != null)
            {
                cachedGroupComp = map.GetComponent<ULS_ControllerGroupMapComponent>();
            }

            return cachedGroupComp;
        }
    }

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

        // 序列化反转模式配置
        List<int> invertedKeys = null;
        List<bool> invertedValues = null;
        Scribe_Collections.Look(ref invertedModeByGroupId, "invertedModeByGroupId", LookMode.Value, LookMode.Value,
            ref invertedKeys, ref invertedValues);

        if (Scribe.mode is LoadSaveMode.PostLoadInit)
        {
            filterTypeByGroupId ??= new();
            invertedModeByGroupId ??= new();
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

    public bool GetGroupInvertedMode(int groupId)
    {
        if (groupId < 1 || invertedModeByGroupId is null)
        {
            return false;
        }

        return invertedModeByGroupId.TryGetValue(groupId, out bool inverted) && inverted;
    }

    public void SetGroupInvertedMode(int groupId, bool inverted)
    {
        if (groupId < 1)
        {
            return;
        }

        invertedModeByGroupId ??= new();
        invertedModeByGroupId[groupId] = inverted;
    }

    public bool ToggleGroupInvertedMode(int groupId)
    {
        bool current = GetGroupInvertedMode(groupId);
        SetGroupInvertedMode(groupId, !current);
        return !current;
    }

    // 在控制器添加/移除 AutoGroupMarker 或 ControllerGroupId 变化时调用
    public void NotifyAutoGroupsDirty()
    {
        autoGroupsDirty = true;
    }

    // 将所有分组分散到 30 个 Tick 内检测，避免单 Tick 耗时过长
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

        ULS_ControllerGroupMapComponent groupComp = GroupComp;
        if (groupComp == null)
        {
            return;
        }

        // Optimization: Use SimplePool to avoid allocations
        List<int> allGroupIds = SimplePool<List<int>>.Get();
        allGroupIds.Clear();
        HashSet<int> aliveAutoGroups = SimplePool<HashSet<int>>.Get();
        aliveAutoGroups.Clear();
        List<int> toRemove = null;
        List<int> filterToRemove = null;

        try
        {
            groupComp.GetAllGroupIds(allGroupIds);

            foreach (var groupId in allGroupIds)
            {
                if (groupId < 1)
                {
                    continue;
                }

                if (!TryGetGroupMarker(groupId, out ULS_AutoGroupMarker marker, out _,
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
            }


            foreach (var kv in runtimeByGroupId)
            {
                if (!aliveAutoGroups.Contains(kv.Key))
                {
                    toRemove ??= SimplePool<List<int>>.Get();
                    toRemove.Add(kv.Key);
                }
            }

            if (toRemove != null)
            {
                foreach (var t in toRemove)
                {
                    runtimeByGroupId.Remove(t);
                }
            }


            if (filterTypeByGroupId != null)
            {
                foreach (var kv in filterTypeByGroupId)
                {
                    if (!aliveAutoGroups.Contains(kv.Key))
                    {
                        filterToRemove ??= SimplePool<List<int>>.Get();
                        filterToRemove.Add(kv.Key);
                    }
                }

                if (filterToRemove != null)
                {
                    foreach (var t in filterToRemove)
                    {
                        filterTypeByGroupId.Remove(t);
                    }
                }
            }
        }
        finally
        {
            allGroupIds.Clear();
            SimplePool<List<int>>.Return(allGroupIds);
            aliveAutoGroups.Clear();
            SimplePool<HashSet<int>>.Return(aliveAutoGroups);

            if (toRemove != null)
            {
                toRemove.Clear();
                SimplePool<List<int>>.Return(toRemove);
            }

            if (filterToRemove != null)
            {
                filterToRemove.Clear();
                SimplePool<List<int>>.Return(filterToRemove);
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

        ULS_ControllerGroupMapComponent groupComp = GroupComp;
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
        foreach (var t in groupCells)
        {
            if (ULS_Utility.TryGetControllerAt(map, t, out Building_WallController c))
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


        foreach (var t in groupCells)
        {
            if (!ULS_Utility.TryGetControllerAt(map, t, out Building_WallController c))
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

    // 切换反转模式后立即触发，使新设置立即生效
    public void ForceProcessAutoGroup(int groupId)
    {
        if (groupId < 1 || map == null)
        {
            return;
        }

        int tick = Find.TickManager.TicksGame;
        TryProcessAutoGroup(groupId, tick, forceProcess: true);
    }

    private void TryProcessAutoGroup(int groupId, int tick, bool forceProcess = false)
    {
        if (!runtimeByGroupId.TryGetValue(groupId, out AutoGroupRuntime runtime))
        {
            return;
        }

        if (!forceProcess && tick < runtime.nextCheckTick)
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


        int membershipHash = ULS_Utility.ComputeMembershipHash(groupCells);
        if (runtime.scanCells == null || runtime.scanCells.Count == 0 || runtime.membershipHash != membershipHash)
        {
            runtime.membershipHash = membershipHash;
            // Replaces BuildScanCells
            UpdateScanCells(runtime, groupCells, props.maxRadius);
        }


        bool hasTarget = false;
        int cellCount = runtime.scanCells.Count;
        int pawnCount = map.mapPawns.AllPawnsSpawnedCount;

        // Optimization: Heuristic strategy
        // If pawn count is small relative to scan area, iterate pawns.
        // Otherwise, iterate scan cells.
        if (pawnCount <= cellCount && runtime.scanCellsSet != null)
        {
            IReadOnlyList<Pawn> pawns = map.mapPawns.AllPawnsSpawned;
            foreach (var p in pawns)
            {
                if (p is { Spawned: true } && runtime.scanCellsSet.Contains(p.Position))
                {
                    if (ULS_AutoGroupUtility.PawnMatchesGroupType(p, filterType))
                    {
                        hasTarget = true;
                        break;
                    }
                }
            }
        }
        else
        {
            foreach (var cell in runtime.scanCells)
            {
                if (!cell.InBounds(map))
                {
                    continue;
                }

                List<Thing> things = map.thingGrid.ThingsListAtFast(cell);
                foreach (var t in things)
                {
                    if (t is Pawn pawn && ULS_AutoGroupUtility.PawnMatchesGroupType(pawn, filterType))
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
        }

        if (hasTarget)
        {
            runtime.lastSeenTick = tick;
        }


        Building_WallController controller = null;
        IntVec3 controllerCell = IntVec3.Invalid;
        bool groupHasAnyStored = false;
        bool groupHasAnyNotStored = false;
        foreach (var t in groupCells)
        {
            if (!ULS_Utility.TryGetControllerAt(map, t, out Building_WallController c))
            {
                continue;
            }

            if (controller == null)
            {
                controller = c;
                controllerCell = t;
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


        // 正常模式：检测到目标 Pawn 时降下（让路）
        // 反转模式：检测到目标 Pawn 时升起（阻挡）
        bool inverted = GetGroupInvertedMode(groupId);
        bool closeWanted = inverted ? !hasTarget : hasTarget;

        if (runtime.lastSeenTick != int.MinValue)
        {
            bool withinDelay = tick - runtime.lastSeenTick < props.closeDelayTicks;
            if (inverted)
            {
                // 反转模式：最近检测到目标时保持升起（不降下）
                if (withinDelay) closeWanted = false;
            }
            else
            {
                // 正常模式：最近检测到目标时保持降下（让路）
                if (withinDelay) closeWanted = true;
            }
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


    private void UpdateScanCells(AutoGroupRuntime runtime, List<IntVec3> groupCells, int maxRadius)
    {
        if (maxRadius < 0) maxRadius = 0;

        if (runtime.scanCells == null)
        {
            runtime.scanCells = new List<IntVec3>();
        }
        else
        {
            runtime.scanCells.Clear();
        }

        if (runtime.scanCellsSet == null)
        {
            runtime.scanCellsSet = new HashSet<IntVec3>();
        }
        else
        {
            runtime.scanCellsSet.Clear();
        }

        if (groupCells != null)
        {
            foreach (var center in groupCells)
            {
                for (int dx = -maxRadius; dx <= maxRadius; dx++)
                {
                    for (int dz = -maxRadius; dz <= maxRadius; dz++)
                    {
                        IntVec3 cell = new IntVec3(center.x + dx, 0, center.z + dz);
                        if (cell.InBounds(map))
                        {
                            if (runtime.scanCellsSet.Add(cell))
                            {
                                runtime.scanCells.Add(cell);
                            }
                        }
                    }
                }
            }
        }
    }
}
