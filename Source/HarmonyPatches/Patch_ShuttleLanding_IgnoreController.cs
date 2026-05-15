using System.Reflection;

namespace Universal_Lift_Structure;

// ============================================================
// 【Harmony 补丁：穿梭机落点放行升降控制器】
// ============================================================
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
// ============================================================
[HarmonyPatch]
public static class Patch_ShuttleLanding_IgnoreController
{
    // 复用反射缓存：DropCellFinder.IsSafeDropSpot 为 private static
    // 该方法签名包含可空类型，必须显式列出参数类型避免歧义
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

    // ============================================================
    // 【Postfix：RoyalTitlePermitWorker_CallShuttle.GetReportFromCell】
    // ============================================================
    // 目的：在原方法判定单元格被阻挡时，复核是否"只剩升降控制器"在挡；
    //       若是则清除阻挡结果，允许穿梭机落于此格。
    //
    // 条件：
    //   - 非物体类阻挡（越界 / 雾化 / 不可走 / 地形承载力不足 / 山体顶板）保持原结果
    //   - 物体类阻挡只放行 Building_WallController；其它建筑、树木、运输物、Skyfaller 仍阻挡
    //
    // 注意：原方法为 private static，使用字符串名补丁
    // ============================================================
    [HarmonyPatch(typeof(RoyalTitlePermitWorker_CallShuttle), "GetReportFromCell")]
    [HarmonyPostfix]
    public static void GetReportFromCell_Postfix(IntVec3 cell, Map map, bool interactionSpot,
        ThingDef shuttleDef, ref string __result)
    {
        // 原方法已放行 → 无需介入
        if (__result == null)
        {
            return;
        }

        // 复核非物体类条件：任一不满足说明阻挡原因与控制器无关，保持原结果
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

        // 扫描格内物体，识别"是否只有控制器在挡"
        List<Thing> thingList = cell.GetThingList(map);
        bool hasController = false;
        for (int i = 0; i < thingList.Count; i++)
        {
            Thing thing = thingList[i];

            // 升降控制器：标记后跳过
            if (thing is Building_WallController)
            {
                hasController = true;
                continue;
            }

            // 与原方法相同的阻挡判定：运输物 / Skyfaller / 非电缆建筑 → 仍阻挡
            if (thing is IActiveTransporter
                || thing is Skyfaller
                || (thing.def.category == ThingCategory.Building && !thing.def.building.isPowerConduit))
            {
                return;
            }

            // 树木仍阻挡
            PlantProperties plant = thing.def.plant;
            if (plant != null && plant.IsTree)
            {
                return;
            }
        }

        // 此格仅由控制器导致阻挡 → 放行
        if (hasController)
        {
            __result = null;
        }
    }

    // ============================================================
    // 【Postfix：DropCellFinder.SkyfallerCanLandAt】
    // ============================================================
    // 目的：系统自动寻找穿梭机落点时（如 TryFindSafeLandingSpotCloseToColony），
    //       同样让控制器单元格不被一票否决；其余阻挡条件保持原行为。
    //
    // 注意：原方法内部调用了 private 的 DropCellFinder.IsSafeDropSpot，
    //       通过反射调用以保证安全条件一致。
    // ============================================================
    [HarmonyPatch(typeof(DropCellFinder), nameof(DropCellFinder.SkyfallerCanLandAt))]
    [HarmonyPostfix]
    public static void SkyfallerCanLandAt_Postfix(IntVec3 c, Map map, IntVec2 size, Faction faction,
        ref bool __result)
    {
        // 原方法已放行 → 无需介入
        if (__result)
        {
            return;
        }

        // 反射不可用时退出（运行时签名变化等极端情况）
        if (IsSafeDropSpotMethod == null)
        {
            return;
        }

        CellRect cellRect = GenAdj.OccupiedRect(c, Rot4.North, size);

        // 越界 → 保持原结果
        if (!cellRect.InBounds(map))
        {
            return;
        }

        // IsSafeDropSpot：透过反射使用与原方法相同的参数（distToEdge=5，其它取默认值）
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

        // 扫描每格物体，识别"是否只有控制器在挡"
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

        // 整片落区仅由控制器导致阻挡 → 放行
        if (hasController)
        {
            __result = true;
        }
    }
}
