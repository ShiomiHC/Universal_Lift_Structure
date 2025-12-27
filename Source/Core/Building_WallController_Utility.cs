namespace Universal_Lift_Structure;

/// 文件意图：Building_WallController - 辅助工具方法。
/// 包含：消息提示、阻挡检测、退款逻辑、其他工具方法。
public partial class Building_WallController
{
    // ==================== 消息提示方法 ====================

    /// 显示拒绝消息（红色）
    private static void MessageReject(string key, LookTargets lookTargets, params NamedArgument[] args)
    {
        Messages.Message(
            key.Translate(args),
            lookTargets,
            MessageTypeDefOf.RejectInput,
            historical: false);
    }

    /// 显示中性消息（白色）
    private static void MessageNeutral(string key, LookTargets lookTargets, params NamedArgument[] args)
    {
        Messages.Message(
            key.Translate(args),
            lookTargets,
            MessageTypeDefOf.NeutralEvent,
            historical: false);
    }

    // ==================== 阻挡检测 ====================

    /// 检查指定位置是否被阻挡，无法升起建筑
    private bool IsBlockedForRaise(Map map, IntVec3 spawnCell, Thing storedThing)
    {
        foreach (IntVec3 cell in GenAdj.OccupiedRect(spawnCell, storedRotation, storedThing.def.size))
        {
            // 检查越界
            if (!cell.InBounds(map))
            {
                return true;
            }

            // 检查建筑阻挡（排除升降阻挡器）
            Building edifice = map.edificeGrid[cell];
            if (edifice != null && edifice.def != ULS_ThingDefOf.ULS_LiftBlocker)
            {
                return true;
            }

            // 检查其他物体阻挡
            List<Thing> things = map.thingGrid.ThingsListAtFast(cell);
            for (int i = 0; i < things.Count; i++)
            {
                Thing thing = things[i];

                // 排除自身、存储物、阻挡器、控制器
                if (thing == this ||
                    thing == storedThing ||
                    thing.def == ULS_ThingDefOf.ULS_LiftBlocker ||
                    IsWallControllerThing(thing))
                {
                    continue;
                }

                // 检查是否是阻挡物
                if (thing is Pawn ||
                    thing is Frame ||
                    thing is Blueprint ||
                    (thing.def.category == ThingCategory.Building &&
                     (thing.def.building == null || thing.def.building.isEdifice)))
                {
                    return true;
                }
            }
        }

        return false;
    }

    /// 判断物体是否是控制器或控制器相关物
    private bool IsWallControllerThing(Thing thing)
    {
        if (thing == null)
        {
            return false;
        }

        if (thing is Building_WallController)
        {
            return true;
        }

        ThingDef def = thing.def;
        if (def == null)
        {
            return false;
        }

        if (def.defName == "ULS_WallController")
        {
            return true;
        }

        if (def.entityDefToBuild != null && def.entityDefToBuild.defName == "ULS_WallController")
        {
            return true;
        }

        return false;
    }

    // ==================== 退款逻辑 ====================

    /// 退款存储的建筑（控制器销毁时调用）
    internal void RefundStored(Map map)
    {
        if (!HasStored)
        {
            return;
        }

        Thing storedThing = StoredThing;
        if (storedThing == null)
        {
            storedCell = IntVec3.Invalid;
            storedThingMarketValueIgnoreHp = 0f;
            return;
        }

        // 从容器移除
        innerContainer.Remove(storedThing);
        storedThingMarketValueIgnoreHp = 0f;

        if (storedThing.Destroyed)
        {
            storedCell = IntVec3.Invalid;
            return;
        }

        // 设置位置和旋转
        IntVec3 position = storedCell.IsValid ? storedCell : Position;
        storedThing.Position = position;
        storedThing.Rotation = storedRotation;

        // 退款
        if (map != null)
        {
            GenSpawn.Refund(storedThing, map, CellRect.Empty);
        }
        else
        {
            storedThing.Destroy();
        }

        storedCell = IntVec3.Invalid;
    }

    // ==================== 配置读取 ====================

    /// 获取组最大规模（从 Mod 设置读取）
    private static int GetGroupMaxSize()
    {
        int maxSize = UniversalLiftStructureMod.Settings?.groupMaxSize ?? 20;
        if (maxSize < 1)
        {
            return 20;
        }
        return maxSize;
    }
}
