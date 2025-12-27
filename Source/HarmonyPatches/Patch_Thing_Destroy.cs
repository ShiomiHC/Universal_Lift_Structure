namespace Universal_Lift_Structure;

/// 文件意图：清理控制台侧生成的 flick 代理，避免控制台被拆除/摧毁后残留不可见物体。
/// 说明：控制器本体已在 `Building_WallController.Destroy` 中显式清理其代理；
/// 但控制台当前仅在 Console 模式“按需”生成代理，因此需要在 Destroy 时做一次清理。
[HarmonyPatch(typeof(Thing), nameof(Thing.Destroy))]
public static class Patch_Thing_Destroy
{
    [HarmonyPostfix]
    public static void Postfix(Thing __instance)
    {
        if (__instance == null)
        {
            return;
        }

        // 仅处理 ULS 控制台。
        if (__instance.def == null || __instance.def.defName != "ULS_LiftConsole")
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
