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
}