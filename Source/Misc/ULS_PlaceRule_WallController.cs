namespace Universal_Lift_Structure;

// 控制器的放置规则限制类 PlaceWorker
// 主要用于防止控制器重叠放置，以及防止放置在被禁止的建筑上
public class ULS_PlaceRule_WallController : PlaceWorker
{
    // 判断是否允许在指定位置放置控制器
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

        // 获取该位置的所有物体列表
        List<Thing> things = loc.GetThingList(map);
        foreach (var t in things)
        {
            // 忽略自身或无效物体
            if (t is null || t == thingToIgnore)
            {
                continue;
            }


            // 1. 禁止在已有控制器上再次放置控制器（防止重叠）
            if (t is Building_WallController)
            {
                return new AcceptanceReport("ULS_CannotPlace_ControllerExists".Translate());
            }

            ThingDef tDef = t.def;


            // 2. 检查蓝图冲突：如果该位置已有同样定义的建筑蓝图，也视为冲突
            if (tDef == checkingThingDef || tDef?.entityDefToBuild == checkingThingDef)
            {
                return new AcceptanceReport("ULS_CannotPlace_ControllerExists".Translate());
            }


            // 3. 检查黑名单限制
            // 如果地上的物体在设置中被列为“禁止在其上建造控制器”，则拒绝放置
            ThingDef builtDef = null;
            if (tDef?.entityDefToBuild is ThingDef entityDefToBuild)
            {
                // 如果是蓝图/框架，取其建成后的Def
                builtDef = entityDefToBuild;
            }
            else if (tDef is { category: ThingCategory.Building })
            {
                // 如果是已有建筑，直接取其Def
                builtDef = tDef;
            }

            // 检查设置中的黑名单
            if (builtDef is not null && settings?.IsDefNameBlacklisted(builtDef.defName) == true)
            {
                return new AcceptanceReport("ULS_CannotPlace_BlacklistedOverlay".Translate(builtDef.defName));
            }
        }

        return true;
    }
}