namespace Universal_Lift_Structure;

/// 文件意图：业务判定工具集。集中维护“是否注入结构侧 Gizmo”的规则与控制器查找逻辑，避免把规则堆在 Harmony Patch 里。

public static class ULS_Utility
{
    /// 方法意图：从指定格子的 thingGrid 中查找 Building_WallController，用于把结构侧按钮绑定到“同格控制器”的实际执行逻辑。
    public static bool TryGetControllerAt(Map map, IntVec3 cell, out Building_WallController controller)
    {
        controller = null;
        if (map == null)
        {
            return false;
        }

        var things = map.thingGrid.ThingsListAtFast(cell);
        for (int i = 0; i < things.Count; i++)
        {
            if (things[i] is Building_WallController found)
            {
                controller = found;
                return true;
            }
        }

        return false;
    }

    // 查找：在指定建筑的占格内找到任意一个控制器（用于处理多格建筑 root cell 与控制器不在同一格的情况）
    public static bool TryGetAnyControllerUnderBuilding(Building building, out Building_WallController controller, out IntVec3 controllerCell)
    {
        controller = null;
        controllerCell = IntVec3.Invalid;

        Map map = building?.Map;
        if (map == null)
        {
            return false;
        }

        CellRect rect = building.OccupiedRect();
        foreach (IntVec3 cell in rect)
        {
            if (TryGetControllerAt(map, cell, out controller))
            {
                controllerCell = cell;
                return true;
            }
        }

        return false;
    }

    public static bool AnyControllerUnderBuildingExcept(Building building, Building_WallController except)
    {
        if (building?.Map == null) return false;
        CellRect rect = building.OccupiedRect();
        foreach (IntVec3 cell in rect)
        {
            if (TryGetControllerAt(building.Map, cell, out Building_WallController controller) && controller != except)
            {
                return true;
            }
        }
        return false;
    }

        
    /// 方法意图：依据 UniversalLiftStructureSettings 判断结构是否应被排除（门/自然岩壁/DefName 白名单未允许）。该判定用于“不给按钮”，而不是在按钮点击后兜底。
    public static bool IsEdificeBlacklisted(Building edifice)
    {
        if (edifice is null)
        {
            return true;
        }

        UniversalLiftStructureSettings settings = UniversalLiftStructureMod.Settings;
        if (settings is null)
        {
            return false;
        }

        if (settings.excludeDoors && edifice is Building_Door)
        {
            return true;
        }

        if (settings.excludeNaturalRock && edifice.def?.building is { isNaturalRock: true })
        {
            return true;
        }

        if (settings.IsDefNameBlacklisted(edifice.def.defName))
        {
            return true;
        }

        return false;
    }

        
    /// 方法意图：综合判定是否应在该结构上注入“降下”按钮：必须是可摧毁的 edifice、非 Frame、且白名单允许（未被排除）。
    public static bool CanInjectLowerGizmo(Building edifice)
    {
        if (edifice is null or { Destroyed: true } or Frame)
        {
            return false;
        }

        ThingDef def = edifice.def;
        if (def is null)
        {
            return false;
        }

        if (def.building is null || !def.building.isEdifice)
        {
            return false;
        }

        if (!def.destroyable)
        {
            return false;
        }

        if (IsEdificeBlacklisted(edifice))
        {
            return false;
        }

        return true;
    }

        
    /// 方法意图：Console 模式下按“距离口径”寻找最近的控制台（不做可达性判断，交由原版派工自然失败/挂起）。
    /// 说明：仅返回玩家派系的控制台。
    public static bool TryGetNearestLiftConsoleByDistance(Map map, IntVec3 origin, out ThingWithComps console)
    {
        console = null;
        if (map == null)
        {
            return false;
        }

        ThingDef consoleDef = DefDatabase<ThingDef>.GetNamedSilentFail("ULS_LiftConsole");
        if (consoleDef == null)
        {
            return false;
        }

        List<Thing> consoles = map.listerThings.ThingsOfDef(consoleDef);
        if (consoles == null || consoles.Count <= 0)
        {
            return false;
        }

        int bestDistSq = int.MaxValue;
        for (int i = 0; i < consoles.Count; i++)
        {
            if (!(consoles[i] is ThingWithComps twc) || !twc.Spawned)
            {
                continue;
            }

            if (twc.Faction != Faction.OfPlayer)
            {
                continue;
            }

            int distSq = origin.DistanceToSquared(twc.Position);
            if (distSq < bestDistSq)
            {
                bestDistSq = distSq;
                console = twc;
            }
        }

        return console != null;
    }
}
