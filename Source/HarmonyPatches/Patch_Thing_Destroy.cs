namespace Universal_Lift_Structure;

[HarmonyPatch(typeof(Thing), nameof(Thing.Destroy))]
public static class Patch_Thing_Destroy
{
    [HarmonyPrefix]
    public static void Prefix(Thing __instance)
    {
        if (__instance == null)
        {
            return;
        }


        if (__instance.def == null || (__instance.def.defName != "ULS_LiftConsole" && !(__instance is Building_WallController)))
        {
            return;
        }

        Map map = __instance.Map;
        if (map == null)
        {
            return;
        }

        IntVec3 cell = __instance.Position;
        if (!cell.IsValid || !cell.InBounds(map))
        {
            return;
        }

        List<Thing> things = map.thingGrid.ThingsListAt(cell);
        if (things == null || things.Count == 0)
        {
            return;
        }

        for (int i = things.Count - 1; i >= 0; i--)
        {
            Thing t = things[i];
            if (t == null || t.Destroyed)
            {
                continue;
            }

            if (t.def == ULS_ThingDefOf.ULS_FlickProxy)
            {
                t.Destroy();
            }
        }
    }
}