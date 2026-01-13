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
    private List<ULS_MultiCellGroupRecord> groupRecords = new();


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
    // 【重建索引】
    // ============================================================
    // 从 groupRecords 重建 groupByRootCell 快速查询索引
    //
    // 【重建流程】
    // 1. 清空 groupByRootCell
    // 2. 防御性检查：groupRecords 为 null 则创建新列表
    // 3. 遍历 groupRecords，跳过无效记录
    // 4. 将每个记录添加到 groupByRootCell 索引
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
    //
    // 【清理操作】
    // - 将 MultiCellGroupRootCell 设为 IntVec3.Invalid
    // - 调用 ClearLiftProcessAndRemoveBlocker() 中断升降流程
    // - 移除升降阻挡器（LiftBlocker）
    //
    // 【为什么需要清理？】
    // - 防止控制器保留过期的多格分组引用
    // - 避免升降流程在分组销毁后继续执行
    // - 确保控制器状态一致性
    //
    // 【参数说明】
    // - record: 分组记录
    // ============================================================
    private void ClearMemberControllerFlags(ULS_MultiCellGroupRecord record)
    {
        Map mapInstance = map;
        if (mapInstance is null || record?.memberControllerCells is null)
        {
            return;
        }

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