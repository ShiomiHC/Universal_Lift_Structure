namespace Universal_Lift_Structure;


/// 文件意图：按“显式组 GroupId”执行接近自动开闭。
/// - 由控制器变体（marker comp）决定该组是否为自动组以及过滤类型。
/// - 统一在 MapComponent 中调度，避免每个控制器单独 Tick。

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

    // 组设置：过滤器（敌/友/中立）。
    // 注意：需要存档；否则玩家通过 Gizmo 设置的过滤器会在读档后丢失。
    private Dictionary<int, ULS_AutoGroupType> filterTypeByGroupId = new();

    // 自动组列表：定期刷新（或由外部通知脏）
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
        Scribe_Collections.Look(ref filterTypeByGroupId, "filterTypeByGroupId", LookMode.Value, LookMode.Value, ref keys, ref values);

        if (Scribe.mode is LoadSaveMode.PostLoadInit && filterTypeByGroupId is null)
        {
            filterTypeByGroupId = new();
        }
    }

    
    /// 方法意图：获取组过滤器。若该组尚无设置，则使用 marker 的默认值并写入。
    
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

        
        /// 方法意图：当控制器生成/销毁/改组后，通知自动组列表需要刷新。
        /// 说明：该方法同时用于“确保 MapComponent 被创建”。
        
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

            // 定期刷新：避免组操作后列表长期过期。
            if (autoGroupsDirty || tick - lastRefreshTick >= 250)
            {
                RefreshAutoGroups(tick);
            }

            if (autoGroupIds.Count == 0)
            {
                return;
            }

            // 轮询：保证每 30 tick 至少扫完一轮（支持 checkIntervalTicks=30 的上限）。
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

            // 清理 runtime：只保留仍存在且仍为自动组的 groupId。
            HashSet<int> aliveAutoGroups = new HashSet<int>();

            for (int i = 0; i < allGroupIds.Count; i++)
            {
                int groupId = allGroupIds[i];
                if (groupId < 1)
                {
                    continue;
                }

                if (!TryGetGroupMarker(groupId, out ULS_AutoGroupMarker marker, out List<IntVec3> groupCells, out string error))
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
                    // 错峰：把首次检查分散到 30 tick 窗口内，避免大量自动组在同一 tick 同时进入“首次扫描”。
                    int phase = groupId % 30;
                    runtimeByGroupId.Add(groupId, new AutoGroupRuntime { nextCheckTick = tick + phase });
                }

                // 轻量初始化：如果组当前处于“未收纳”，视为已打开，避免一开始就触发一次 raise。
                if (groupCells != null && groupCells.Count > 0 && ULS_Utility.TryGetControllerAt(map, groupCells[0], out Building_WallController c) && c != null)
                {
                }
            }

            // 移除失效 runtime
            List<int> toRemove = null;
            foreach (var kv in runtimeByGroupId)
            {
                if (!aliveAutoGroups.Contains(kv.Key))
                {
                    if (toRemove == null) toRemove = new List<int>();
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

            // 清理失效的过滤器设置：避免 groupId 被复用后继承旧设置。
            if (filterTypeByGroupId != null)
            {
                List<int> filterToRemove = null;
                foreach (var kv in filterTypeByGroupId)
                {
                    if (!aliveAutoGroups.Contains(kv.Key))
                    {
                        if (filterToRemove == null) filterToRemove = new List<int>();
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

            // 防御：索引落在有效范围。
            if (autoGroupIndex >= autoGroupIds.Count)
            {
                autoGroupIndex = 0;
            }
        }

        private bool TryGetGroupMarker(int groupId, out ULS_AutoGroupMarker marker, out List<IntVec3> groupCells, out string error)
        {
            marker = null;
            groupCells = null;
            error = null;

            ULS_ControllerGroupMapComponent groupComp = map.GetComponent<ULS_ControllerGroupMapComponent>();
            if (groupComp == null)
            {
                return false;
            }

            if (!groupComp.TryGetGroupControllerCells(groupId, out groupCells) || groupCells == null || groupCells.Count == 0)
            {
                return false;
            }

            // 找到一个代表控制器
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

            // 校验混组：组内所有控制器必须同为“自动控制器”（不允许自动/手动混组）。
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

            // 组过滤器：由 Gizmo 设置；若未设置则使用 Def 默认值。
            ULS_AutoGroupType filterType = GetOrInitGroupFilterType(groupId, props.autoGroupType);

            // 计算成员 hash；变化时重建扫描格缓存。
            int membershipHash = ComputeMembershipHash(groupCells);
            if (runtime.scanCells == null || runtime.scanCells.Count == 0 || runtime.membershipHash != membershipHash)
            {
                runtime.membershipHash = membershipHash;
                runtime.scanCells = BuildScanCells(groupCells, props.maxRadius);
            }

            // 扫描目标（早停）：任意格发现符合条件的 Pawn 立即返回。
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

            // 组级状态：不能仅看“代表控制器”，否则在“部分升起/部分被阻挡”的场景下会误判整组状态并导致后续不再处理。
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
                    // 快速退出：状态已确定为“混合”。
                    // （controller 已找到，用于后续触发动作）
                    break;
                }
            }

            if (controller == null)
            {
                return;
            }

            // 反转语义：检测到 -> 降下/收纳（打开通路）；未检测到 -> 升起/放出（关闭通路）。
            bool closeWanted = hasTarget;
            if (!closeWanted && runtime.lastSeenTick != int.MinValue)
            {
                // 迟滞：目标离开后延迟一段时间再“升起/放出”。
                closeWanted = tick - runtime.lastSeenTick < props.closeDelayTicks;
            }

            if (tick < runtime.nextToggleAllowedTick)
            {
                return;
            }

            // 语义（反转后）：
            // - closeWanted==true：希望“降下/收纳”（即 controller.HasStored==true）
            // - closeWanted==false：希望“升起/放出”（即 controller.HasStored==false）
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
                    // 自动升起：all-or-nothing，且仅在成功执行时进入冷却。
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
