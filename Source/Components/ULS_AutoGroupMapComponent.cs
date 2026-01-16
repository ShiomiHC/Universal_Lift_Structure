namespace Universal_Lift_Structure;

// ============================================================
// 【自动分组检测与控制组件】
// ============================================================
// 此组件负责实现自动分组功能：基于 Pawn 存在检测自动升降控制器
//
// 【继承关系】
// - 继承自 MapComponent：RimWorld 的地图组件基类，提供生命周期管理
//
// 【核心职责】
// 1. 自动检测：定期扫描分组周围是否有符合条件的 Pawn
// 2. 自动控制：根据检测结果自动升起或降下控制器
// 3. 过滤器管理：为每个分组维护独立的 Pawn 类型过滤器
// 4. 性能优化：分散检测任务、缓存扫描区域、智能选择检测策略
//
// 【什么是自动分组？】
// - 带有 ULS_AutoGroupMarker 组件的控制器
// - 可以根据 Pawn 接近/离开自动升降
// - 类似于自动门，但支持分组操作
//
// 【工作机制】
// 1. 扫描阶段：
//    - 在控制器周围一定半径内扫描 Pawn
//    - 根据过滤器类型判断 Pawn 是否符合条件
//    - 例如：只检测殖民者、敌人、或所有人类
//
// 2. 决策阶段：
//    - 如果检测到符合条件的 Pawn：降下控制器（让 Pawn 通过）
//    - 如果一段时间未检测到 Pawn：升起控制器（阻挡通道）
//    - 支持延迟关闭（closeDelayTicks）
//
// 3. 执行阶段：
//    - 调用控制器的 AutoLowerGroup() 或 AutoRaiseGroup()
//    - 支持冷却时间（toggleCooldownTicks）防止频繁切换
//
// 【性能优化策略】
// 1. 分散检测：
//    - 将所有自动分组分散到 30 个 Tick 内检测
//    - 每个 Tick 只检测一部分分组
//    - 避免在同一 Tick 检测所有分组导致卡顿
//
// 2. 启发式扫描：
//    - 如果 Pawn 数量 < 扫描区域大小：遍历 Pawn 列表
//    - 如果扫描区域大小 < Pawn 数量：遍历扫描区域
//    - 动态选择更高效的检测方式
//
// 3. 扫描区域缓存：
//    - 缓存每个分组的扫描区域（scanCells）
//    - 使用 membershipHash 检测分组成员变化
//    - 只在分组成员改变时重建扫描区域
//
// 4. 池化内存管理：
//    - 使用 SimplePool 管理临时列表和 HashSet
//    - 避免频繁的内存分配和 GC 压力
//
// 【数据结构】
// - filterTypeByGroupId：Dictionary<int, ULS_AutoGroupType>
//   用途：存储每个分组的 Pawn 过滤器类型（会被序列化）
//
// - autoGroupIds：List<int>
//   用途：活跃的自动分组 ID 列表（运行时缓存）
//
// - runtimeByGroupId：Dictionary<int, AutoGroupRuntime>
//   用途：存储每个分组的运行时数据（扫描区域、时间戳等）
//
// 【AutoGroupRuntime 内部类】
// 存储每个自动分组的运行时数据：
// - membershipHash：分组成员哈希值（检测成员变化）
// - scanCells：扫描区域单元格列表
// - scanCellsSet：扫描区域单元格 HashSet（快速查找）
// - lastSeenTick：上次检测到 Pawn 的 Tick
// - nextToggleAllowedTick：下次允许切换的 Tick（冷却）
// - nextCheckTick：下次检测的 Tick（检测间隔）
//
// 【检测间隔与时间管理】
// - checkIntervalTicks：检测间隔（由 CompProperties 定义）
// - toggleCooldownTicks：切换冷却时间
// - closeDelayTicks：关闭延迟时间
// - 不同分组使用相位偏移（phase = groupId % 30）避免同步
//
// 【使用方式】
// - 通过 map.GetComponent<ULS_AutoGroupMapComponent>() 获取实例
// - 控制器添加 ULS_AutoGroupMarker 组件后自动生效
// - 通过 Gizmo 设置过滤器类型
// - 系统自动在 MapComponentTick() 中检测和控制
// ============================================================

public class ULS_AutoGroupMapComponent : MapComponent
{
    // ============================================================
    // 【内部类：AutoGroupRuntime】
    // ============================================================
    // 存储单个自动分组的运行时数据和缓存
    // ============================================================
    private class AutoGroupRuntime
    {
        // 【成员哈希值】用于检测分组成员是否变化
        // 如果成员变化，需要重建扫描区域
        public int membershipHash;

        // 【扫描区域缓存】
        // scanCells：扫描区域单元格列表（用于遍历）
        public List<IntVec3> scanCells;

        // scanCellsSet：扫描区域单元格 HashSet（用于快速查找）
        // 启发式策略时使用：判断 Pawn 位置是否在扫描区域内
        public HashSet<IntVec3> scanCellsSet;

        // 【时间戳】
        // lastSeenTick：上次检测到符合条件的 Pawn 的 Tick
        // 用于延迟关闭逻辑
        public int lastSeenTick = int.MinValue;

        // nextToggleAllowedTick：下次允许切换的 Tick
        // 实现冷却机制，防止频繁切换
        public int nextToggleAllowedTick;

        // nextCheckTick：下次执行检测的 Tick
        // 实现检测间隔
        public int nextCheckTick;
    }


    // ============================================================
    // 【字段说明】
    // ============================================================

    // --- 过滤器配置（会被序列化） ---
    // 存储每个分组的 Pawn 过滤器类型
    // 例如：Colonists、Enemies、AllHumans 等
    private Dictionary<int, ULS_AutoGroupType> filterTypeByGroupId = new();

    // --- 反转模式配置（会被序列化） ---
    // 存储每个分组是否启用反转模式
    // 正常模式：检测到目标Pawn时降下（让路），无目标时升起（阻挡）
    // 反转模式：检测到目标Pawn时升起（阻挡），无目标时降下（让路）
    private Dictionary<int, bool> invertedModeByGroupId = new();


    // --- 活跃分组列表（运行时缓存） ---
    // 所有需要处理的自动分组 ID 列表
    private readonly List<int> autoGroupIds = new();

    // 当前检测索引（用于分散检测）
    private int autoGroupIndex;

    // 分组列表是否需要刷新
    private bool autoGroupsDirty = true;

    // 上次刷新分组列表的 Tick
    private int lastRefreshTick;

    // --- 运行时数据缓存 ---
    // 存储每个分组的运行时数据（扫描区域、时间戳等）
    private readonly Dictionary<int, AutoGroupRuntime> runtimeByGroupId = new();

    // --- 组件缓存 ---
    // 缓存的控制器分组管理组件引用
    private ULS_ControllerGroupMapComponent cachedGroupComp;

    // ============================================================
    // 【分组管理组件属性】
    // ============================================================
    // 懒惰初始化的 ControllerGroupMapComponent 引用
    //
    // 【缓存机制】
    // - 第一次访问时从地图获取组件
    // - 后续访问直接返回缓存的引用
    // - 避免重复 GetComponent 调用
    // ============================================================
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

    // ============================================================
    // 【构造函数】
    // ============================================================
    // 创建地图组件实例
    //
    // 【参数说明】
    // - map: 所属地图
    // ============================================================
    public ULS_AutoGroupMapComponent(Map map) : base(map)
    {
    }

    // ============================================================
    // 【序列化方法】
    // ============================================================

    // ============================================================
    // 【序列化/反序列化】
    // ============================================================
    // 保存和加载过滤器配置数据
    //
    // 【序列化内容】
    // - filterTypeByGroupId：使用 LookMode.Value 序列化键和值
    // - 需要两个临时列表 keys 和 values 作为参数
    //
    // 【不序列化的数据】
    // - autoGroupIds：运行时重建
    // - runtimeByGroupId：运行时重建
    // - 这些都是缓存数据，可以从分组信息重新构建
    //
    // 【加载后处理】
    // - 检查 filterTypeByGroupId 是否为 null
    // - 如果为 null 则创建新字典
    // ============================================================
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


    // ============================================================
    // 【过滤器管理方法】
    // ============================================================

    // ============================================================
    // 【获取或初始化过滤器类型】
    // ============================================================
    // 获取指定分组的过滤器类型，如果不存在则使用默认值
    //
    // 【懒惰初始化】
    // - 如果分组没有设置过滤器，使用 defaultType 并保存
    // - 确保每个分组都有有效的过滤器配置
    //
    // 【参数说明】
    // - groupId: 分组ID
    // - defaultType: 默认过滤器类型
    //
    // 【返回值】
    // - 过滤器类型
    // ============================================================
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

    // ============================================================
    // 【尝试获取过滤器类型】
    // ============================================================
    // 查询指定分组的过滤器类型
    //
    // 【参数说明】
    // - groupId: 分组ID
    // - type: 输出：过滤器类型
    //
    // 【返回值】
    // - true 如果查询成功；否则 false
    // ============================================================
    public bool TryGetGroupFilterType(int groupId, out ULS_AutoGroupType type)
    {
        type = default;
        if (groupId < 1 || filterTypeByGroupId is null)
        {
            return false;
        }

        return filterTypeByGroupId.TryGetValue(groupId, out type);
    }

    // ============================================================
    // 【设置过滤器类型】
    // ============================================================
    // 为指定分组设置 Pawn 过滤器类型
    //
    // 【使用场景】
    // - Gizmo 设置界面
    // - 玩家通过 UI 修改过滤器配置
    //
    // 【参数说明】
    // - groupId: 分组ID
    // - type: 过滤器类型
    // ============================================================
    public void SetGroupFilterType(int groupId, ULS_AutoGroupType type)
    {
        if (groupId < 1)
        {
            return;
        }

        filterTypeByGroupId ??= new();
        filterTypeByGroupId[groupId] = type;
    }


    // ============================================================
    // 【获取反转模式】
    // ============================================================
    // 获取指定分组是否启用反转模式
    //
    // 【参数说明】
    // - groupId: 分组ID
    //
    // 【返回值】
    // - true: 反转模式已启用
    // ============================================================
    public bool GetGroupInvertedMode(int groupId)
    {
        if (groupId < 1 || invertedModeByGroupId is null)
        {
            return false;
        }

        return invertedModeByGroupId.TryGetValue(groupId, out bool inverted) && inverted;
    }

    // ============================================================
    // 【设置反转模式】
    // ============================================================
    // 为指定分组设置反转模式
    //
    // 【参数说明】
    // - groupId: 分组ID
    // - inverted: 是否启用反转模式
    // ============================================================
    public void SetGroupInvertedMode(int groupId, bool inverted)
    {
        if (groupId < 1)
        {
            return;
        }

        invertedModeByGroupId ??= new();
        invertedModeByGroupId[groupId] = inverted;
    }

    // ============================================================
    // 【切换反转模式】
    // ============================================================
    // 切换指定分组的反转模式状态
    //
    // 【参数说明】
    // - groupId: 分组ID
    //
    // 【返回值】
    // - 切换后的状态（true=反转模式已启用）
    // ============================================================
    public bool ToggleGroupInvertedMode(int groupId)
    {
        bool current = GetGroupInvertedMode(groupId);
        SetGroupInvertedMode(groupId, !current);
        return !current;
    }


    // ============================================================
    // 【通知分组列表需要刷新】
    // ============================================================
    // 标记自动分组列表为脏数据，下次 Tick 时将重新扫描
    //
    // 【调用时机】
    // - 控制器添加/移除 ULS_AutoGroupMarker 组件
    // - 控制器的 ControllerGroupId 改变
    // - 分组成员发生变化
    // ============================================================
    public void NotifyAutoGroupsDirty()
    {
        autoGroupsDirty = true;
    }

    // ============================================================
    // 【核心Tick方法】★★★ 最重要 ★★★
    // ============================================================

    // ============================================================
    // 【地图组件Tick】★★★ 核心方法 ★★★
    // ============================================================
    // 每个 Tick 调用，负责分散处理所有自动分组的检测
    //
    // 【执行流程】
    // 1. 检查是否需要刷新分组列表：
    //    - autoGroupsDirty 为 true
    //    - 或距上次刷新超过 250 Ticks
    //    → 调用 RefreshAutoGroups() 重新扫描分组
    //
    // 2. 分散检测策略：
    //    - 计算 groupsPerTick = (autoGroupIds.Count + 29) / 30
    //    - 每个 Tick 检测 groupsPerTick 个分组
    //    - 将所有分组分散到 30 个 Tick 内完成
    //    - 使用 autoGroupIndex 轮询遍历分组列表
    //
    // 3. 每个分组的检测：
    //    - 调用 TryProcessAutoGroup() 处理单个分组
    //    - 检测 Pawn 并决定是否需要升/降控制器
    //
    // 【性能优化】
    // - 分散检测避免单个 Tick 耗时过长
    // - 如果有 120 个分组，每个 Tick 检测 4 个分组
    // - 最坏情况下每个分组 30 Ticks 检测一次
    //
    // 【刷新策略】
    // - 每 250 Ticks 强制刷新一次
    // - 或者当 autoGroupsDirty 被设置时刷新
    // - 确保新添加的控制器及时生效
    // ============================================================
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


    // ============================================================
    // 【强制处理指定分组】
    // ============================================================
    // 立即触发指定分组的升降判定（用于反转模式切换后立即生效）
    //
    // 【调用场景】
    // - 切换反转模式后立即触发
    // - 手动触发某分组的检测
    //
    // 【参数说明】
    // - groupId: 分组ID
    // ============================================================
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

        // 如果不是强制处理，检查是否到达下次检测时间
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


        // 判断是否启用反转模式
        // 正常模式：检测到目标Pawn时降下（让路）
        // 反转模式：检测到目标Pawn时升起（阻挡）
        bool inverted = GetGroupInvertedMode(groupId);
        bool closeWanted = inverted ? !hasTarget : hasTarget;

        // 延迟关闭逻辑（仅在正常模式下生效）
        // 反转模式下，延迟逻辑适用于"保持升起"状态
        if (runtime.lastSeenTick != int.MinValue)
        {
            bool withinDelay = tick - runtime.lastSeenTick < props.closeDelayTicks;
            if (inverted)
            {
                // 反转模式：如果最近检测到目标，保持升起（不降下）
                if (withinDelay) closeWanted = false;
            }
            else
            {
                // 正常模式：如果最近检测到目标，保持降下（让路）
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