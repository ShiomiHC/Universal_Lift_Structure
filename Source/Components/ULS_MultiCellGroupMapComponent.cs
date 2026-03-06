namespace Universal_Lift_Structure;

// ============================================================
// 【多格建筑分组管理组件】
// ============================================================
// 此组件负责管理多格建筑的控制器分组记录
//
// 【继承关系】
// - 继承自 MapComponent：RimWorld 的地图组件基类，提供生命周期管理
//
// 【核心职责】
// 1. 分组记录管理：维护所有多格建筑的分组记录（ULS_MultiCellGroupRecord）
// 2. 快速查询：提供基于 rootCell 的 O(1) 查询
// 3. 分组生命周期：处理分组的创建、销毁和退款
// 4. 标志位清理：在分组销毁时清理成员控制器的多格标志
//
// 【什么是多格建筑？】
// - 占据多个单元格的建筑（如 2x2、3x3 的建筑）
// - 例如：大型墙体、多格门等
//
// 【什么是多格分组？】
// - 当多个"单格控制器"管理同一个"多格建筑"时形成的分组
// - 例如：一个 2x2 的建筑可能有 4 个单格控制器，它们需要协同工作
//
// 【数据结构】
// - groupRecords：List<ULS_MultiCellGroupRecord>
//   用途：存储所有分组记录（会被序列化）
//
// - groupByRootCell：Dictionary<IntVec3, ULS_MultiCellGroupRecord>
//   用途：基于 rootCell 的快速查询索引（运行时缓存，不序列化）
//
// 【rootCell 的概念】
// - 每个多格建筑有一个唯一的 rootCell（根单元格）
// - 通常是建筑占据的多个单元格中的第一个（如左下角）
// - 用作分组的唯一标识符
//
// 【分组记录内容】
// ULS_MultiCellGroupRecord 包含：
// - rootCell：多格建筑的根单元格
// - masterControllerCell：主控制器位置
// - memberControllerCells：所有成员控制器位置列表
// - storedDef：收纳的建筑 Def
// - storedStuff：收纳的建筑材料
//
// 【索引重建机制】
// - 索引数据不序列化，只序列化 groupRecords
// - 加载后从 groupRecords 重建 groupByRootCell 索引
// - 通过 RebuildIndex() 方法实现
//
// 【成员标志位清理】
// - 当分组被移除时，需要清理成员控制器的 MultiCellGroupRootCell 标志
// - 通过 ClearMemberControllerFlags() 实现
// - 防止控制器保留过期的多格分组引用
//
// 【退款机制】
// - RefundAndRemoveGroup()：销毁分组前退还收纳的建筑材料
// - 优先从主控制器退款
// - 如果主控制器不存在，从任意成员控制器退款
// - 确保玩家不会因为分组销毁而损失资源
//
// 【使用方式】
// - 通过 map.GetComponent<ULS_MultiCellGroupMapComponent>() 获取实例
// - 控制器创建多格分组时调用 TryAddGroup()
// - 查询分组时调用 TryGetGroup()
// - 销毁分组时调用 RemoveGroup() 或 RefundAndRemoveGroup()
// ============================================================

public class ULS_MultiCellGroupMapComponent : MapComponent
{
    // ============================================================
    // 【字段说明】
    // ============================================================

    // --- 核心数据:分组记录列表 ---
    // 存储所有多格建筑的分组记录（会被序列化）
    internal List<ULS_MultiCellGroupRecord> groupRecords = new();

    // ============================================================
    // 【飞船转移后延迟重建任务列队】
    // ============================================================
    // 当主控被传送时，因为其他组员可能还没生成，所以在下一帧 Tick 中统一执行重建
    private HashSet<Building_WallController> pendingRebuildControllers = new HashSet<Building_WallController>();

    public void RegisterPendingRebuild(Building_WallController controller)
    {
        if (controller != null)
        {
            pendingRebuildControllers.Add(controller);
        }
    }

    // --- 运行时索引：快速查询缓存 ---
    // rootCell → GroupRecord 映射（不被序列化，加载后重建）
    private readonly Dictionary<IntVec3, ULS_MultiCellGroupRecord> groupByRootCell = new();

    // ============================================================
    // 【构造函数】
    // ============================================================
    // 创建地图组件实例
    //
    // 【参数说明】
    // - map: 所属地图
    // ============================================================
    public ULS_MultiCellGroupMapComponent(Map map) : base(map)
    {
    }

    // ============================================================
    // 【序列化与索引管理】
    // ============================================================

    // ============================================================
    // 【序列化方法】
    // ============================================================
    // 保存和加载分组记录数据
    //
    // 【序列化内容】
    // - groupRecords：使用 LookMode.Deep 完整保存分组记录
    // - groupByRootCell 不被序列化（运行时缓存）
    //
    // 【加载后处理】
    // - 调用 RebuildIndex() 重建 groupByRootCell 索引
    // - 从 groupRecords 恢复快速查询能力
    // ============================================================
    public override void ExposeData()
    {
        base.ExposeData();
        Scribe_Collections.Look(ref groupRecords, "ulsMultiCellGroups", LookMode.Deep);

        if (Scribe.mode is LoadSaveMode.PostLoadInit)
        {
            RebuildIndex();
        }
    }

    // ============================================================
    // 【重建索引与单向状态派发】
    // ============================================================
    // 从 groupRecords 重建 groupByRootCell 快速查询索引
    // 并向该地图上的所有成员控制器单向派发（注入）多格组组长坐标
    //
    // 【重建流程】
    // 1. 清空 groupByRootCell
    // 2. 防御性检查：groupRecords 为 null 则创建新列表
    // 3. 遍历 groupRecords 添加到快查字典索引
    // 4. (SSOT重写) 主动为每个记录中的成员控制器强行设置 Runtime 的 MultiCellGroupRootCell
    //
    // 【调用时机】
    // - PostLoadInit：存档加载完成后
    // - 任何需要确保索引同步的情况
    // ============================================================
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

    // ============================================================
    // 【地图加载完成后的最终初始化】
    // ============================================================
    // 在此时刻，所有 Thing 均已完成 SpawnSetup（已加入 map.thingGrid）
    // ============================================================
    public override void FinalizeInit()
    {
        base.FinalizeInit();
        
        // 交由专门的兼容类，检查旧存档补足成员名单，并完成 SSOT 的组坐标运行时派发
        ULS_BackwardCompatibility.CheckAndFixLegacyGroups(this, map);
    }

    // ============================================================
    // 【引擎组件每帧滴答 (Tick)】
    // ============================================================
    // 处理需要在固定游戏时间尺度循环内运行的轻量任务（例如飞船落地后重建排队）
    // ============================================================
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

    // ============================================================
    // 【查询方法】
    // ============================================================

    // ============================================================
    // 【检查分组是否存在】
    // ============================================================
    // 快速检查指定 rootCell 是否有对应的多格分组
    //
    // 【参数说明】
    // - rootCell: 多格建筑的根单元格
    //
    // 【返回值】
    // - true 如果分组存在；否则 false
    // ============================================================
    public bool HasGroup(IntVec3 rootCell)
    {
        return rootCell.IsValid && groupByRootCell.ContainsKey(rootCell);
    }

    // ============================================================
    // 【获取分组记录】
    // ============================================================
    // 根据 rootCell 查询对应的多格分组记录
    //
    // 【参数说明】
    // - rootCell: 多格建筑的根单元格
    // - record: 输出：分组记录对象
    //
    // 【返回值】
    // - true 如果找到分组；否则 false
    // ============================================================
    public bool TryGetGroup(IntVec3 rootCell, out ULS_MultiCellGroupRecord record)
    {
        record = null;
        if (!rootCell.IsValid)
        {
            return false;
        }

        return groupByRootCell.TryGetValue(rootCell, out record);
    }

    // ============================================================
    // 【分组管理】
    // ============================================================

    // ============================================================
    // 【添加分组】
    // ============================================================
    // 将新的多格分组记录添加到管理列表
    //
    // 【验证逻辑】
    // - 检查记录是否为 null
    // - 检查 rootCell 是否有效
    // - 检查是否已存在同一 rootCell 的分组
    //
    // 【添加操作】
    // - 同时添加到 groupRecords 和 groupByRootCell
    // - 保持数据与索引同步
    //
    // 【参数说明】
    // - record: 要添加的分组记录
    //
    // 【返回值】
    // - true 如果添加成功；false 如果记录无效或已存在
    // ============================================================
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

    // ============================================================
    // 【移除分组】
    // ============================================================
    // 从管理列表中移除指定的多格分组并清理成员标志
    //
    // 【移除流程】
    // 1. 查找分组记录
    // 2. 调用 ClearMemberControllerFlags() 清理成员标志
    // 3. 从 groupByRootCell 移除索引
    // 4. 从 groupRecords 移除记录
    //
    // 【清理成员标志】
    // - 清空所有成员控制器的 MultiCellGroupRootCell
    // - 调用 ClearLiftProcessAndRemoveBlocker() 中断升降流程
    // - 防止控制器保留过期的多格分组引用
    //
    // 【参数说明】
    // - rootCell: 要移除的分组的 rootCell
    // ============================================================
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

    // ============================================================
    // 【退款并移除分组】★★★ 核心方法 ★★★
    // ============================================================
    // 在移除分组前，退还收纳的建筑材料给玩家
    //
    // 【退款逻辑】
    // 1. 查找分组记录
    // 2. 优先尝试从主控制器退款（masterControllerCell）
    // 3. 如果主控制器不存在，遍历成员控制器
    // 4. 找到第一个有收纳建筑的控制器并退款
    // 5. 调用 RemoveGroup() 清理分组
    //
    // 【退款方法】
    // - 调用控制器的 RefundStored(map) 方法
    // - 在控制器位置生成材料物品
    //
    // 【使用场景】
    // - 控制器被销毁
    // - 玩家手动拆散多格分组
    // - 确保玩家不会损失资源
    //
    // 【参数说明】
    // - rootCell: 要移除的分组的 rootCell
    // ============================================================
    public void RefundAndRemoveGroup(IntVec3 rootCell)
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
                master.RefundStored(mapInstance);
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

                    controller.RefundStored(mapInstance);
                    break;
                }
            }
        }

        RemoveGroup(rootCell);
    }

    // ============================================================
    // 【清理成员控制器标志】
    // ============================================================
    // 清空分组所有成员控制器的多格分组标志和状态
    // (SSOT 变更: 主控也应该在这里被一并清空状态)
    //
    // 【清理操作】
    // - 将 MultiCellGroupRootCell 设为 IntVec3.Invalid
    // - 调用 ClearLiftProcessAndRemoveBlocker() 中断升降流程
    // - 移除升降阻挡器（LiftBlocker）
    // ============================================================
    private void ClearMemberControllerFlags(ULS_MultiCellGroupRecord record)
    {
        Map mapInstance = map;
        if (mapInstance is null || record == null)
        {
            return;
        }

        // 1. 清理主控本身（在 SSOT 重构后，主控不再属于 memberControllerCells 名单，所以必须单列清理）
        if (record.masterControllerCell.IsValid && ULS_Utility.TryGetControllerAt(mapInstance, record.masterControllerCell, out Building_WallController masterController))
        {
            masterController.MultiCellGroupRootCell = IntVec3.Invalid;
            masterController.ClearLiftProcessAndRemoveBlocker();
        }

        // 2. 清理所有成员
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