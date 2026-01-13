namespace Universal_Lift_Structure;

// ============================================================
// 【控制器分组索引管理组件】
// ============================================================
// 此组件负责在地图级别管理所有控制器的分组关系，提供快速查询和操作
//
// 【继承关系】
// - 继承自 MapComponent：RimWorld 的地图组件基类，提供生命周期管理
//
// 【核心职责】
// 1. 分组索引：维护 GroupId ↔ ControllerCells 的双向映射
// 2. ID 分配：自动分配唯一的 GroupId（优先使用 1-1000 范围）
// 3. 分组操作：注册、移除、合并分组
// 4. 查询服务：根据 GroupId 查询所有控制器位置
//
// 【索引结构】
// - controllerCellsByGroupId：GroupId → List<IntVec3>（一对多）
//   用途：根据 GroupId 快速获取该组的所有控制器位置
//
// - groupIdByControllerCell：IntVec3 → GroupId（一对一）
//   用途：根据控制器位置快速查询其所属的 GroupId
//
// 【双向映射机制】
// - 两个字典必须保持同步！
// - 注册控制器时：同时更新两个字典
// - 移除控制器时：同时清理两个字典
// - 通过双向映射实现 O(1) 时间复杂度的双向查询
//
// 【ID 分配策略】
// 1. 优先分配 1-1000 范围内的最小可用 ID
// 2. 如果 1-1000 全部占用，使用 maxId + 1
// 3. 确保每个 ID 全局唯一
//
// 【索引重建机制】
// - 不序列化索引数据！（索引是运行时缓存）
// - 从地图上的实际控制器重建索引
// - 在以下时机重建：
//   1. FinalizeInit()：地图初始化完成后
//   2. PostLoadInit：存档加载完成后
//
// 【为什么不序列化索引？】
// - 索引可以从控制器数据重建，序列化会浪费空间
// - 避免索引与实际数据不同步的风险
// - 控制器的 ControllerGroupId 字段已被序列化，是唯一数据源
//
// 【使用方式】
// - 通过 map.GetComponent<ULS_ControllerGroupMapComponent>() 获取实例
// - 控制器 Spawn 时调用 RegisterOrUpdateController()
// - 控制器 DeSpawn 时调用 RemoveControllerCell()
// - Gizmo 操作时调用 MergeGroups() 或 AssignControllerCellsToGroup()
// ============================================================

public class ULS_ControllerGroupMapComponent : MapComponent
{
    // ============================================================
    // 【字段说明】
    // ============================================================

    // --- 核心索引字典 ---
    // 双向映射，必须保持同步！

    // GroupId → ControllerCells 映射（一对多）
    // 存储每个分组ID对应的所有控制器位置列表
    private readonly Dictionary<int, List<IntVec3>> controllerCellsByGroupId = new();

    // ControllerCell → GroupId 映射（一对一）
    // 存储每个控制器位置对应的分组ID
    private readonly Dictionary<IntVec3, int> groupIdByControllerCell = new();

    // --- 状态标志 ---

    // 索引是否已构建完成
    private bool indexBuilt;

    // 是否正在重建索引（防止递归调用）
    private bool rebuildInProgress;

    // ============================================================
    // 【构造函数】
    // ============================================================
    // 创建地图组件实例
    //
    // 【参数】
    // - map: 所属地图
    // ============================================================
    public ULS_ControllerGroupMapComponent(Map map) : base(map)
    {
    }

    // ============================================================
    // 【生命周期方法】
    // ============================================================

    // ============================================================
    // 【序列化方法】
    // ============================================================
    // 不序列化索引数据，仅标记索引需要重建
    //
    // 【重要设计决策】
    // - 索引数据不序列化！
    // - 索引完全从控制器的 ControllerGroupId 字段重建
    // - 加载后将 indexBuilt 设为 false，触发后续重建
    // ============================================================
    public override void ExposeData()
    {
        base.ExposeData();

        if (Scribe.mode is LoadSaveMode.PostLoadInit)
        {
            indexBuilt = false;
        }
    }

    // ============================================================
    // 【地图初始化完成】
    // ============================================================
    // 在地图初始化完成后重建索引
    //
    // 【调用时机】
    // - 新游戏开始时
    // - 存档加载完成后
    // - 确保所有控制器已 Spawn 到地图上
    // ============================================================
    public override void FinalizeInit()
    {
        base.FinalizeInit();
        RebuildIndexFromMap();
    }

    // ============================================================
    // 【确保索引已构建】
    // ============================================================
    // 惰性初始化：如果索引未构建且不在重建中，则触发重建
    //
    // 【防御性编程】
    // - 在任何查询操作前调用此方法
    // - 确保索引始终可用
    // - 避免在重建过程中递归调用
    // ============================================================
    private void EnsureIndex()
    {
        if (indexBuilt || rebuildInProgress)
        {
            return;
        }

        RebuildIndexFromMap();
    }

    // ============================================================
    // 【ID 分配方法】
    // ============================================================

    // ============================================================
    // 【创建新的 GroupId】★★★ 核心方法 ★★★
    // ============================================================
    // 自动分配一个全局唯一的 GroupId
    //
    // 【分配策略】
    // 1. 优先分配 1-1000 范围内的最小可用 ID
    //    - 这个范围对用户友好，易于记忆和输入
    //    - 遍历 1-1000 查找第一个未占用的 ID
    //
    // 2. 如果 1-1000 全部占用（罕见情况）
    //    - 使用 maxExistingGroupId + 1
    //    - 记录警告日志
    //
    // 【性能考虑】
    // - 最坏情况：O(1000) 遍历
    // - 实际情况：通常在前几次尝试就能找到可用 ID
    // - 此方法调用频率不高（仅在创建新分组时）
    //
    // 【返回值】
    // - 新的唯一 GroupId
    // ============================================================
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

    // ============================================================
    // 【索引注册与移除】
    // ============================================================

    // ============================================================
    // 【注册或更新控制器】★★★ 核心方法 ★★★
    // ============================================================
    // 将控制器添加到分组索引，或更新其分组关系
    //
    // 【自动 ID 分配】
    // - 如果控制器的 ControllerGroupId < 1，自动分配新 ID
    // - 确保每个控制器都有有效的 GroupId
    //
    // 【分组切换处理】
    // - 如果控制器已在索引中但 GroupId 改变：
    //   1. 从旧分组中移除
    //   2. 添加到新分组
    //   3. 如果旧分组为空，清理旧分组记录
    //
    // 【双向映射同步】
    // - 同时更新 groupIdByControllerCell 和 controllerCellsByGroupId
    // - 保持两个字典完全同步
    //
    // 【调用时机】
    // - 控制器 Spawn 时（SpawnSetup）
    // - 玩家手动修改控制器的 GroupId
    // - 自动分组操作时
    //
    // 【参数说明】
    // - controller: 要注册的控制器
    // ============================================================
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

    // ============================================================
    // 【移除控制器】
    // ============================================================
    // 从分组索引中移除指定位置的控制器
    //
    // 【清理逻辑】
    // 1. 从 groupIdByControllerCell 中移除映射
    // 2. 从对应分组的控制器列表中移除
    // 3. 如果分组变为空，清理整个分组记录
    //
    // 【调用时机】
    // - 控制器 DeSpawn 时（DeSpawn）
    // - 控制器被销毁时
    //
    // 【参数说明】
    // - cell: 控制器位置
    // ============================================================
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

    // ============================================================
    // 【查询方法】
    // ============================================================

    // ============================================================
    // 【获取分组的所有控制器位置】
    // ============================================================
    // 根据 GroupId 查询该组的所有控制器位置
    //
    // 【重要警告】
    // - 返回的 cells 是内部列表的直接引用，不是副本！
    // - 调用者不应修改返回的列表
    // - 如需修改，请先复制到临时列表
    //
    // 【性能】
    // - O(1) 字典查询
    // - 无内存分配
    //
    // 【参数说明】
    // - groupId: 分组ID
    // - cells: 输出：控制器位置列表（直接引用内部列表）
    //
    // 【返回值】
    // - true 如果找到分组；否则 false
    // ============================================================
    public bool TryGetGroupControllerCells(int groupId, out List<IntVec3> cells)
    {
        EnsureIndex();
        return controllerCellsByGroupId.TryGetValue(groupId, out cells);
    }


    // ============================================================
    // 【获取所有分组ID】
    // ============================================================
    // 将所有已存在的分组ID添加到输出列表
    //
    // 【使用场景】
    // - UI 显示所有可用分组
    // - 遍历所有分组进行批量操作
    //
    // 【注意】
    // - 会先清空 outGroupIds
    // - 调用者负责池化管理 outGroupIds
    //
    // 【参数说明】
    // - outGroupIds: 接收分组ID的输出列表（调用者提供）
    // ============================================================
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

    // ============================================================
    // 【分组操作方法】
    // ============================================================

    // ============================================================
    // 【批量分配控制器到分组】
    // ============================================================
    // 将多个控制器位置分配到指定分组
    //
    // 【自动 ID 分配】
    // - 如果 groupId < 1，自动创建新的 GroupId
    //
    // 【批量处理】
    // - 遍历所有位置，逐个更新控制器的 GroupId
    // - 跳过无效位置和不存在的控制器
    // - 调用 RegisterOrUpdateController 更新索引
    //
    // 【使用场景】
    // - Gizmo "合并分组" 操作
    // - 自动分组功能
    // - 批量重新分配控制器
    //
    // 【参数说明】
    // - controllerCells: 要分配的控制器位置列表
    // - groupId: 目标分组ID（如果 < 1 则自动创建新ID）
    // ============================================================
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

    // ============================================================
    // 【合并分组】
    // ============================================================
    // 将源分组的所有控制器合并到目标分组
    //
    // 【参数验证】
    // - 两个 ID 都必须 >= 1
    // - 不能合并自己到自己
    // - 源分组必须存在且非空
    //
    // 【合并流程】
    // 1. 获取源分组的所有控制器位置
    // 2. 复制到临时列表（使用 PooledList 避免分配）
    // 3. 调用 AssignControllerCellsToGroup 批量重新分配
    // 4. 源分组会自动清空（因为控制器都被移除了）
    //
    // 【为什么要复制？】
    // - sourceCells 是内部列表的引用
    // - AssignControllerCellsToGroup 会修改索引
    // - 在遍历过程中修改源列表会导致问题
    //
    // 【使用场景】
    // - Gizmo "合并分组" 操作
    // - 玩家选择多个控制器合并到一组
    //
    // 【参数说明】
    // - targetGroupId: 目标分组ID
    // - sourceGroupId: 源分组ID（合并后将被清空）
    // ============================================================
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

    // ============================================================
    // 【索引重建】
    // ============================================================

    // ============================================================
    // 【从地图重建索引】★★★ 核心方法 ★★★
    // ============================================================
    // 扫描地图上的所有控制器，重建完整的分组索引
    //
    // 【重建流程】
    // 1. 设置 rebuildInProgress 标志，防止递归
    // 2. 清空所有索引字典
    // 3. 扫描地图上的所有建筑（BuildingArtificial）
    // 4. 筛选出 Building_WallController 类型
    // 5. 为每个控制器分配或验证 ControllerGroupId
    // 6. 调用 RegisterOrUpdateController 添加到索引
    // 7. 设置 indexBuilt = true，完成重建
    //
    // 【ID 分配】
    // - 如果控制器的 ControllerGroupId < 1，自动分配新 ID
    // - 这处理了旧存档或新建控制器的情况
    //
    // 【调用时机】
    // - FinalizeInit()：地图初始化完成后
    // - PostLoadInit：存档加载完成后
    // - EnsureIndex()：惰性初始化时
    //
    // 【性能】
    // - 只在初始化时调用一次
    // - 之后通过增量更新维护索引
    // ============================================================
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