namespace Universal_Lift_Structure;

/// 文件意图：放置规则。
/// - 禁止控制器与自身（或蓝图/框架）叠放，确保一格仅存一个控制器。
/// - 禁止控制器与“白名单未允许”的建筑（或蓝图/框架）叠放，避免在不可收纳物上放置控制器。
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

        List<Thing> things = loc.GetThingList(map);
        for (int i = 0; i < things.Count; i++)
        {
            Thing t = things[i];
            if (t is null || t == thingToIgnore)
            {
                continue;
            }

            // 禁止与自身控制器（实体）叠放
            if (t is Building_WallController)
            {
                return new AcceptanceReport("ULS_CannotPlace_ControllerExists".Translate());
            }

            ThingDef tDef = t.def;

            // 禁止与自身控制器（蓝图/框架/其它中间态）叠放
            if (tDef == checkingThingDef || tDef?.entityDefToBuild == checkingThingDef)
            {
                return new AcceptanceReport("ULS_CannotPlace_ControllerExists".Translate());
            }

            // 白名单叠放限制（未允许）：只对“建筑实体”与“建筑的建造中间态（entityDefToBuild）”生效
            // 说明：实现上仍以 settings.IsDefNameBlacklisted(builtDef.defName) 作为“未允许”判定。
            // 说明：白名单 UI 当前仅提供可摧毁 edifice 的候选，但玩家也可能手动输入其它 defName。
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
}
