namespace Universal_Lift_Structure;

// 桥版升降控制器附带的"桥地形开关"行为。
// 在 raise 流程开始时铺设桥地形（保证后续 spawn 的重型建筑获得 Heavy affordance），
// 在 lower 流程完成时撤除桥地形（恢复水面，与船只 mod 通行判定兼容）。
// 注：BridgeBase 继承链的地形 isFoundation=true，因此 SetTerrain 实际走 SetFoundation；
//      RemoveTopLayer 检测到 foundation 时走 RemoveFoundation。topGrid 始终保留原水面。
public class CompProperties_ULS_Bridge : CompProperties
{
    public CompProperties_ULS_Bridge()
    {
        compClass = typeof(Comp_ULS_Bridge);
    }
}


public class Comp_ULS_Bridge : ThingComp
{
    // 新建造完成时立即进入"桥铺设态"（默认初始态）。
    // respawningAfterLoad=true 时地形已由存档恢复，不主动覆盖。
    public override void PostSpawnSetup(bool respawningAfterLoad)
    {
        base.PostSpawnSetup(respawningAfterLoad);

        if (!respawningAfterLoad)
        {
            EnsureBridgeLaid();
        }
    }


    // 控制器消失时（玩家拆除 / 被破坏 / 转移地图前）兜底撤桥，避免"桥残留"在空 cell 上误导玩家。
    //
    // 真摧毁分支（KillFinalize / Deconstruct / FailConstruction）额外做 edifice 级联：
    //   桥版控制器与其升起态的墙 edifice 在语义上是一个整体，控制器损毁时墙必须跟随消失，
    //   否则会出现"墙悬浮在水面上"的视觉异常。统一走 Deconstruct（50% 返还），与 DestroyStored 对齐。
    // 非真摧毁分支（Vanish / WillReplace / QuestLogic 等）仅撤桥，保留原有的转场/卸图行为。
    public override void PostDeSpawn(Map map, DestroyMode mode = DestroyMode.Vanish)
    {
        base.PostDeSpawn(map, mode);

        if (map == null)
        {
            return;
        }

        IntVec3 pos = parent.Position;
        if (!pos.InBounds(map))
        {
            return;
        }

        bool isRealDestroy = mode is DestroyMode.KillFinalize
                                  or DestroyMode.Deconstruct
                                  or DestroyMode.FailConstruction;
        if (isRealDestroy)
        {
            Building edifice = pos.GetEdifice(map);
            if (edifice != null && edifice != parent && !edifice.Destroyed)
            {
                edifice.Destroy(DestroyMode.Deconstruct);
            }
        }

        TerrainGrid grid = map.terrainGrid;
        if (grid != null && grid.FoundationAt(pos) == ULS_TerrainDefOf.ULS_LiftBridge)
        {
            grid.RemoveTopLayer(pos, doLeavings: false);
        }
    }


    // raise 开始钩子：由 Building_WallController_LiftProcess.BeginLiftProcess(Raising,...) 调用。
    // 必须在 spawn stored 建筑前铺好桥，否则 spawn 出来的重型建筑会因 affordance 不足而失败。
    public void OnRaiseStarted(Building_WallController controller)
    {
        EnsureBridgeLaid();
    }


    // lower 完成钩子：由 Building_WallController_LiftProcess.TickLiftProcess 在 Lowering 完成时调用。
    // 此时控制器已吞下原本在桥上的 edifice，撤桥让水面恢复。
    public void OnLowerCompleted(Building_WallController controller)
    {
        RemoveBridgeIfPresent();
    }


    private void EnsureBridgeLaid()
    {
        Map map = parent.Map;
        if (map == null)
        {
            return;
        }

        IntVec3 pos = parent.Position;
        if (!pos.InBounds(map))
        {
            return;
        }

        TerrainGrid grid = map.terrainGrid;
        if (grid == null || grid.FoundationAt(pos) == ULS_TerrainDefOf.ULS_LiftBridge)
        {
            return;
        }

        grid.SetTerrain(pos, ULS_TerrainDefOf.ULS_LiftBridge);
    }


    private void RemoveBridgeIfPresent()
    {
        Map map = parent.Map;
        if (map == null)
        {
            return;
        }

        IntVec3 pos = parent.Position;
        if (!pos.InBounds(map))
        {
            return;
        }

        TerrainGrid grid = map.terrainGrid;
        if (grid == null || grid.FoundationAt(pos) != ULS_TerrainDefOf.ULS_LiftBridge)
        {
            return;
        }

        grid.RemoveTopLayer(pos, doLeavings: false);
    }
}
