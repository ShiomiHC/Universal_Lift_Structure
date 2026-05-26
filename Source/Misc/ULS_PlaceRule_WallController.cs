namespace Universal_Lift_Structure;

public class ULS_PlaceRule_WallController : PlaceWorker
{
    public override AcceptanceReport AllowsPlacing(
        BuildableDef checkingDef,
        IntVec3 loc,
        Rot4 rot,
        Map map,
        Thing thingToIgnore = null,
        Thing thing = null)
    {
        if (map is null)
        {
            return false;
        }

        ThingDef checkingThingDef = checkingDef as ThingDef;
        UniversalLiftStructureSettings settings = UniversalLiftStructureMod.Settings;

        // 桥版分支：声明了 Comp_ULS_Bridge 的控制器必须建在水面（IsWater==true）。
        // 桥版 ThingDef 通过 IsNull 移除 terrainAffordanceNeeded，由此处接管唯一的地形约束。
        if (HasBridgeComp(checkingThingDef))
        {
            TerrainDef terrain = map.terrainGrid?.TerrainAt(loc);
            if (terrain is null || !terrain.IsWater)
            {
                return new AcceptanceReport("ULS_CannotPlace_BridgeNeedsWater".Translate());
            }
        }

        List<Thing> things = loc.GetThingList(map);
        foreach (var t in things)
        {
            if (t is null || t == thingToIgnore)
            {
                continue;
            }

            if (t is Building_WallController)
            {
                return new AcceptanceReport("ULS_CannotPlace_ControllerExists".Translate());
            }

            ThingDef tDef = t.def;

            // 同位置已有同定义的蓝图也视为冲突
            if (tDef == checkingThingDef || tDef?.entityDefToBuild == checkingThingDef)
            {
                return new AcceptanceReport("ULS_CannotPlace_ControllerExists".Translate());
            }

            ThingDef builtDef = null;
            if (tDef?.entityDefToBuild is ThingDef entityDefToBuild)
            {
                builtDef = entityDefToBuild;
            }
            else if (tDef is { category: ThingCategory.Building })
            {
                builtDef = tDef;
            }

            if (builtDef is not null && settings?.IsDefNameBlacklisted(builtDef.defName) == true)
            {
                return new AcceptanceReport("ULS_CannotPlace_BlacklistedOverlay".Translate(builtDef.defName));
            }
        }

        return true;
    }


    private static bool HasBridgeComp(ThingDef def)
    {
        if (def?.comps == null)
        {
            return false;
        }

        foreach (var compProps in def.comps)
        {
            if (compProps is CompProperties_ULS_Bridge)
            {
                return true;
            }
        }

        return false;
    }
}
