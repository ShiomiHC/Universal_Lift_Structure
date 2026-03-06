
namespace Universal_Lift_Structure;

// ============================================================
// 【向前兼容与重构时序处理类 (SSOT)】
// ============================================================
// 此类用于处理由于大一统（SSOT）架构引发的读档时序问题，
// 以及处理旧版本存档中缺少重要关联数据的情况。
// ============================================================
public static class ULS_BackwardCompatibility
{
    // ============================================================
    // 【核心方法：检查旧组并派发 SSOT 属性】
    // ============================================================
    // 调用时机：ULS_MultiCellGroupMapComponent.FinalizeInit()
    // 此时刻地图所有的 Thing 都已存在于 map.thingGrid 中。
    // ============================================================
    public static void CheckAndFixLegacyGroups(ULS_MultiCellGroupMapComponent comp, Map map)
    {
        if (comp == null || map == null) return;

        // 复制一份名册防止在迭代中修改
        List<ULS_MultiCellGroupRecord> records = new List<ULS_MultiCellGroupRecord>();
        if (comp.groupRecords != null)
        {
            records.AddRange(comp.groupRecords);
        }

        foreach (var record in records)
        {
            if (record == null || !record.rootCell.IsValid)
                continue;

            // 尝试获取主控制器
            if (!ULS_Utility.TryGetControllerAt(map, record.masterControllerCell, out Building_WallController master))
            {
                continue; // 没找到主控制器，跳过
            }

            // 1. 旧存档兼容修复：
            // 如果是极早期的存档，Record 中的 memberControllerCells 可能是空的（因为以前小弟各记各的）。
            // 为了适配 SSOT 架构，我们需要为该 Record 补全小弟名单，并利用 StoredThing 的占位来寻找所有成员。
            bool needsFix = (record.memberControllerCells == null || record.memberControllerCells.Count == 0);

            if (needsFix && master.HasStored && master.StoredThing.def.size != IntVec2.One)
            {
                Log.Message($"[ULS 向后兼容] 检测到早期多格组记录缺失小弟名册 ({record.rootCell})。正在基于真实 footprint 重建...");

                record.memberControllerCells = new List<IntVec3>();
                CellRect footprint = GenAdj.OccupiedRect(master.Position, master.storedRotation, master.StoredThing.def.size);

                foreach (IntVec3 cell in footprint)
                {
                    // 剔除组长自己
                    if (cell != master.Position)
                    {
                        record.memberControllerCells.Add(cell);
                    }
                }
                Log.Message($"[ULS 向后兼容] 补全完毕。该多格组补回了 {record.memberControllerCells.Count} 名成员。");
            }

            // 2. SSOT 单点事实属性派发：
            // 将 ULS_MultiCellGroupMapComponent 里的身份，统一单向硬注入到每个控制器的运行内存。
            // 同时标脏对应格子，确保 Thing.Print 重新判断隐藏状态（静态网格渲染缓存问题）。

            // 派发给主控制器
            master.MultiCellGroupRootCell = record.rootCell;
            map.mapDrawer?.MapMeshDirty(master.Position, MapMeshFlagDefOf.Things);
            map.mapDrawer?.MapMeshDirty(master.Position, MapMeshFlagDefOf.Buildings);

            // 派发给所有名册上的小弟
            if (record.memberControllerCells != null)
            {
                foreach (IntVec3 memberCell in record.memberControllerCells)
                {
                    if (ULS_Utility.TryGetControllerAt(map, memberCell, out Building_WallController member))
                    {
                        member.MultiCellGroupRootCell = record.rootCell;
                        map.mapDrawer?.MapMeshDirty(memberCell, MapMeshFlagDefOf.Things);
                        map.mapDrawer?.MapMeshDirty(memberCell, MapMeshFlagDefOf.Buildings);
                    }
                    else
                    {
                        Log.Warning($"[ULS 向后兼容] SSOT派发异常：多格组成员格 {memberCell} 上未找到控制器！是否发生了跨存档环境变动？");
                    }
                }
            }
        }
    }
}