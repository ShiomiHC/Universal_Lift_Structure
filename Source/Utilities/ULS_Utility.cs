namespace Universal_Lift_Structure;

public static class ULS_Utility
{
    public static bool TryGetControllerAt(Map map, IntVec3 cell, out Building_WallController controller)
    {
        controller = null;
        if (map == null)
        {
            return false;
        }

        var things = map.thingGrid.ThingsListAtFast(cell);
        foreach (var t in things)
        {
            if (t is Building_WallController found)
            {
                controller = found;
                return true;
            }
        }

        return false;
    }


    public static bool TryGetAnyControllerUnderBuilding(Building building, out Building_WallController controller,
        out IntVec3 controllerCell)
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

        if (edifice is Building_Door || edifice.def?.IsDoor == true)
        {
            return true;
        }

        if (settings.excludeNaturalRock && edifice.def?.building is { isNaturalRock: true })
        {
            return true;
        }

        if (settings.IsDefNameBlacklisted(edifice.def?.defName))
        {
            return true;
        }

        return false;
    }


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


    public static bool TryGetNearestLiftConsoleByDistance(Map map, IntVec3 origin, out ThingWithComps console)
    {
        console = null;
        if (map == null)
        {
            return false;
        }

        ThingDef consoleDef = ULS_ThingDefOf.ULS_LiftConsole;
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
        foreach (var t in consoles)
        {
            if (!(t is ThingWithComps twc) || !twc.Spawned)
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
                var power = twc.GetComp<CompPowerTrader>();
                if (power is { PowerOn: false })
                {
                    continue;
                }

                bestDistSq = distSq;
                console = twc;
            }
        }

        return console != null;
    }


    // 计算一组单元格坐标的成员哈希值（用于缓存校验）
    public static int ComputeMembershipHash(List<IntVec3> cells)
    {
        unchecked
        {
            int h = 17;
            if (cells != null)
            {
                foreach (var c in cells)
                {
                    h = h * 31 + c.x;
                    h = h * 31 + c.z;
                }
            }

            return h;
        }
    }
}