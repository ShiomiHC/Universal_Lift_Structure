namespace Universal_Lift_Structure;

// 背景：
//   ULS_LiftBridge 继承自 BridgeBase（isFoundation=true / destroyBuildingsOnDestroyed=true），
//   损毁走 TerrainGrid.Notify_FoundationDestroyed。vanilla 在该方法尾部调用
//   c.GetFirstBuilding(map)?.Kill()——按 thingList 顺序取第一个 Building 并 Kill（0% 返还）。
//
// 问题：
//   桥版控制器（Building_WallController, isEdifice=false）与升起态的墙 edifice 同居一格。
//   - 升起态被炸：vanilla 通常先命中墙 edifice → Kill；控制器与桥地形残留（视觉异常）。
//   - 收纳态被炸：vanilla 命中控制器 → Kill（0% 返还）。
//   两种情形都偏离"控制器损毁应走 50% Deconstruct"的统一语义。
//
// 方案：
//   Prefix 全权接管 ULS_LiftBridge 这一类 foundation 的销毁路径：
//   1. 找到该格的桥版控制器（带 Comp_ULS_Bridge），调用 controller.Destroy(Deconstruct)。
//      其 PostDeSpawn 内含级联逻辑：级联墙 edifice + RemoveTopLayer 撤桥。
//   2. 复刻 vanilla 的 destroyEffect / destroyEffectWater 视觉效果。
//   3. 复刻 vanilla 的 CheckAutoRebuildTerrainOnDestroyed。
//   4. return false 让 vanilla 完全跳过本调用——避免其再用 Kill() 把控制器/edifice 改成 0% 路径。
//
// 边界：
//   - 非 ULS_LiftBridge 的 foundation 一律 return true，由 vanilla 处理。
//   - 异常态（该格没有桥版控制器但桥地形存在）走 fallback：手动销毁 edifice + RemoveFoundation。
//   - Notify_TerrainDestroyed(c)（vanilla 中 CanRemoveTopLayerAt 分支）省略：桥下 topGrid 是水，
//     CanRemoveTopLayerAt(水) 返回 false，原本就是 no-op。
[HarmonyPatch(typeof(TerrainGrid), nameof(TerrainGrid.Notify_FoundationDestroyed))]
public static class Patch_TerrainGrid_BridgeFoundationDestroyed
{
    // TerrainGrid.map 为 private 字段，反射访问器。
    private static readonly AccessTools.FieldRef<TerrainGrid, Map> MapRef =
        AccessTools.FieldRefAccess<TerrainGrid, Map>("map");

    public static bool Prefix(TerrainGrid __instance, IntVec3 c)
    {
        TerrainDef foundation = __instance.FoundationAt(c);
        if (foundation != ULS_TerrainDefOf.ULS_LiftBridge)
        {
            return true;
        }

        Map map = MapRef(__instance);
        if (map == null || !c.InBounds(map))
        {
            return true;
        }

        // 主路径：找桥控制器 → Deconstruct，其 PostDeSpawn 负责级联墙 + 撤桥
        Building_WallController controller = FindBridgeController(c, map);
        if (controller != null && !controller.Destroyed)
        {
            controller.Destroy(DestroyMode.Deconstruct);
        }
        else
        {
            // 异常 fallback：缺控制器时手动收尾
            Building edifice = c.GetEdifice(map);
            if (edifice != null && !edifice.Destroyed)
            {
                edifice.Destroy(DestroyMode.Deconstruct);
            }
            if (__instance.FoundationAt(c) == ULS_TerrainDefOf.ULS_LiftBridge)
            {
                __instance.RemoveFoundation(c, doLeavings: false);
            }
        }

        // 复刻 vanilla destroy effect：在水面用 destroyEffectWater，其余用 destroyEffect
        TerrainDef topNow = __instance.TerrainAt(c);
        EffecterDef effDef = (foundation.destroyEffectWater != null && topNow != null && topNow.IsWater)
            ? foundation.destroyEffectWater
            : foundation.destroyEffect;
        if (effDef != null)
        {
            Effecter eff = effDef.Spawn();
            eff.Trigger(new TargetInfo(c, map), new TargetInfo(c, map));
            eff.Cleanup();
        }

        ThingUtility.CheckAutoRebuildTerrainOnDestroyed(foundation, c, map);
        return false;
    }

    private static Building_WallController FindBridgeController(IntVec3 c, Map map)
    {
        List<Thing> things = map.thingGrid.ThingsListAt(c);
        for (int i = 0; i < things.Count; i++)
        {
            if (things[i] is Building_WallController wc && wc.GetComp<Comp_ULS_Bridge>() != null)
            {
                return wc;
            }
        }
        return null;
    }
}
