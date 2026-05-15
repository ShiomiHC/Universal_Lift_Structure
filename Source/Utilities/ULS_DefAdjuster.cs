namespace Universal_Lift_Structure;

// 运行时动态修改 Def 字段的工具类
// 职责：根据 Mod 设置项向目标 ThingDef 应用或撤销运行时字段覆盖
// 约束：所有方法仅在 DefDatabase 加载完毕后调用（LongEventHandler.ExecuteWhenFinished 或之后）
public static class ULS_DefAdjuster
{
    private static readonly string[] ControllerDefNames =
    {
        "ULS_WallController",
        "ULS_WallController_Auto"
    };

    private const string ConsoleDefName = "ULS_LiftConsole";

    // 根据设置项决定是否剥夺目标建筑的血量逻辑
    // - protectController：控制器（WallController / Auto）是否免疫
    // - protectConsole：升降控制台是否免疫（仅在 protectController 开启时生效）
    public static void ApplyImmunity(bool protectController, bool protectConsole)
    {
        foreach (string defName in ControllerDefNames)
        {
            ApplyToDef(defName, !protectController);
        }

        ApplyToDef(ConsoleDefName, !(protectController && protectConsole));
    }

    // 设置单个 def 的 useHitPoints；
    // 恢复为 true 时顺带扫描所有已加载地图，修复 HP ≤ 0 的实例。
    // Thing.hitPointsInt 初始默认值为 -1（反编译确认），
    // 保护期间存档后 health 节点不被写入，加载后该字段维持 -1。
    private static void ApplyToDef(string defName, bool useHitPoints)
    {
        ThingDef def = DefDatabase<ThingDef>.GetNamedSilentFail(defName);
        if (def == null)
        {
            return;
        }

        def.useHitPoints = useHitPoints;

        if (useHitPoints)
        {
            TryRestoreHpAllMaps(def);
        }
    }

    // 供 MapComponent.FinalizeInit 调用：地图加载/进入时修复该地图上所有相关建筑的 HP
    // 无条件运行，检查廉价；保护开启期间 useHitPoints = false，修复后的值不会被显示，无副作用
    public static void TryRestoreHpOnMap(Map map)
    {
        foreach (string defName in ControllerDefNames)
        {
            ThingDef def = DefDatabase<ThingDef>.GetNamedSilentFail(defName);
            if (def != null)
            {
                RestoreHpOnMap(def, map);
            }
        }

        ThingDef consoleDef = DefDatabase<ThingDef>.GetNamedSilentFail(ConsoleDefName);
        if (consoleDef != null)
        {
            RestoreHpOnMap(consoleDef, map);
        }
    }

    private static void TryRestoreHpAllMaps(ThingDef def)
    {
        if (Current.Game == null)
        {
            return;
        }

        foreach (Map map in Find.Maps)
        {
            RestoreHpOnMap(def, map);
        }
    }

    private static void RestoreHpOnMap(ThingDef def, Map map)
    {
        foreach (Thing thing in map.listerThings.ThingsOfDef(def))
        {
            if (thing.HitPoints <= 0)
            {
                thing.HitPoints = thing.MaxHitPoints;
            }
        }
    }
}
