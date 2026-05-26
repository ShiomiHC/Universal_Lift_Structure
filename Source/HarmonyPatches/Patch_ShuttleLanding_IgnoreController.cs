using System.Reflection;

namespace Universal_Lift_Structure;

// 背景：
//   奥德赛 DLC 的穿梭机落点选择器（MapParent.GetShuttleFloatMenuOptions）
//   通过 RoyalTitlePermitWorker_CallShuttle.ShuttleCanLandHere → GetReportFromCell
//   逐格校验落点；该校验把任意 ThingCategory.Building 都视为阻挡
//   （仅放行 isPowerConduit=true 的电缆类建筑）。
//
//   本 Mod 的升降控制器（Building_WallController）是贴附在墙上的"薄覆盖物"，
//   语义上不应阻挡穿梭机；但它既不是电缆，也不是 edifice，
//   导致即便对应的墙体已收纳（单元格可走），控制器单独存在仍会让校验失败。
//
// 设计取舍：
//   1. 不动 Def（不强行设 isPowerConduit=true，避免被电网/电弧/墙体替换等系统误判）
//   2. 通过 Postfix 在校验失败时复核：若该格"只剩控制器在挡"，则放行
//   3. 墙体若仍存在（升起态），由香草逻辑天然阻挡，无需特殊处理
//   4. Building_LiftBlocker（升降过程中的临时占位）保持原行为——不放行，避免与升降过程撞车
[HarmonyPatch]
public static class Patch_ShuttleLanding_IgnoreController
{
    // DropCellFinder.IsSafeDropSpot 为 private static，签名含可空类型，必须显式列出参数类型
    private static readonly MethodInfo IsSafeDropSpotMethod = AccessTools.Method(
        typeof(DropCellFinder),
        "IsSafeDropSpot",
        new[]
        {
            typeof(IntVec3),
            typeof(Map),
            typeof(Faction),
            typeof(IntVec2?),
            typeof(int),
            typeof(int),
            typeof(int),
            typeof(IntVec3?)
        });

    // 原方法为 private static，使用字符串名补丁。
    // 原方法判定阻挡后复核：若唯一阻挡物是 Building_WallController 则放行；越界/雾化/不可走/顶板等非物体阻挡保持原结果。
    [HarmonyPatch(typeof(RoyalTitlePermitWorker_CallShuttle), "GetReportFromCell")]
    [HarmonyPostfix]
    public static void GetReportFromCell_Postfix(IntVec3 cell, Map map, bool interactionSpot,
        ThingDef shuttleDef, ref string __result)
    {
        if (__result == null)
        {
            return;
        }

        if (!cell.InBounds(map))
        {
            return;
        }

        if (cell.Fogged(map))
        {
            return;
        }

        if (!cell.Walkable(map))
        {
            return;
        }

        if (!cell.GetAffordances(map).Contains(ThingDefOf.Shuttle.terrainAffordanceNeeded))
        {
            return;
        }

        RoofDef roof = cell.GetRoof(map);
        if (roof != null && (roof.isNatural || roof.isThickRoof))
        {
            return;
        }

        List<Thing> thingList = cell.GetThingList(map);
        bool hasController = false;
        for (int i = 0; i < thingList.Count; i++)
        {
            Thing thing = thingList[i];

            if (thing is Building_WallController)
            {
                hasController = true;
                continue;
            }

            if (thing is IActiveTransporter
                || thing is Skyfaller
                || (thing.def.category == ThingCategory.Building && !thing.def.building.isPowerConduit))
            {
                return;
            }

            PlantProperties plant = thing.def.plant;
            if (plant != null && plant.IsTree)
            {
                return;
            }
        }

        if (hasController)
        {
            __result = null;
        }
    }

    // 自动落点搜索时同样放行控制器格（如 TryFindSafeLandingSpotCloseToColony）。
    // 内部通过反射调用 private DropCellFinder.IsSafeDropSpot 以保证安全条件一致。
    [HarmonyPatch(typeof(DropCellFinder), nameof(DropCellFinder.SkyfallerCanLandAt))]
    [HarmonyPostfix]
    public static void SkyfallerCanLandAt_Postfix(IntVec3 c, Map map, IntVec2 size, Faction faction,
        ref bool __result)
    {
        if (__result)
        {
            return;
        }

        if (IsSafeDropSpotMethod == null)
        {
            return;
        }

        CellRect cellRect = GenAdj.OccupiedRect(c, Rot4.North, size);

        if (!cellRect.InBounds(map))
        {
            return;
        }

        // distToEdge=5 与原方法一致
        object safeDropResult = IsSafeDropSpotMethod.Invoke(null, new object[]
        {
            c,
            map,
            faction,
            (IntVec2?)size,
            5,
            35,
            15,
            (IntVec3?)null
        });
        if (safeDropResult is not bool isSafe || !isSafe)
        {
            return;
        }

        bool hasController = false;
        foreach (IntVec3 item in cellRect)
        {
            List<Thing> thingList = item.GetThingList(map);
            for (int i = 0; i < thingList.Count; i++)
            {
                Thing thing = thingList[i];

                if (thing is Building_WallController)
                {
                    hasController = true;
                    continue;
                }

                if (thing is IActiveTransporter || thing is Skyfaller)
                {
                    return;
                }

                if (thing.def.preventSkyfallersLandingOn)
                {
                    return;
                }

                ThingCategory category = thing.def.category;
                if (category == ThingCategory.Item || category == ThingCategory.Building)
                {
                    return;
                }
            }
        }

        if (hasController)
        {
            __result = true;
        }
    }
}
