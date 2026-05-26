
namespace Universal_Lift_Structure;

// 处理旧版存档缺少关联数据的时序问题（SSOT 架构迁移）
public static class ULS_BackwardCompatibility
{
    // 调用时机：ULS_MultiCellGroupMapComponent.FinalizeInit()，此时所有 Thing 已存在于 map.thingGrid
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

            if (!ULS_Utility.TryGetControllerAt(map, record.masterControllerCell, out Building_WallController master))
            {
                continue;
            }

            // 旧存档兼容修复：极早期存档的 memberControllerCells 可能为空（以前小弟各记各的），基于 footprint 重建
            bool needsFix = (record.memberControllerCells == null || record.memberControllerCells.Count == 0);

            if (needsFix && master.HasStored && master.StoredThing.def.size != IntVec2.One)
            {
                Log.Message($"[ULS 向后兼容] 检测到早期多格组记录缺失小弟名册 ({record.rootCell})。正在基于真实 footprint 重建...");

                record.memberControllerCells = new List<IntVec3>();
                CellRect footprint = GenAdj.OccupiedRect(master.Position, master.storedRotation, master.StoredThing.def.size);

                foreach (IntVec3 cell in footprint)
                {
                    if (cell != master.Position)
                    {
                        record.memberControllerCells.Add(cell);
                    }
                }
                Log.Message($"[ULS 向后兼容] 补全完毕。该多格组补回了 {record.memberControllerCells.Count} 名成员。");
            }

            // SSOT 派发：将 GroupMapComponent 的身份单向注入到各控制器运行内存，并标脏格子（静态网格缓存问题）
            master.MultiCellGroupRootCell = record.rootCell;
            map.mapDrawer?.MapMeshDirty(master.Position, MapMeshFlagDefOf.Things);
            map.mapDrawer?.MapMeshDirty(master.Position, MapMeshFlagDefOf.Buildings);

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