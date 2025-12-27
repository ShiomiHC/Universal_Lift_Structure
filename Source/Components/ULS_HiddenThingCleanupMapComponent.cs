namespace Universal_Lift_Structure;

/// 文件意图：读档/进地图后清理隐藏物（`ULS_FlickProxy`、`ULS_LiftBlocker`）的错位与孤儿实例。
///
/// 清理原则（开发测试阶段，失败应尽快暴露）：
/// - `ULS_FlickProxy`：只允许存在于“合法 ownerCell”（控制器格、控制台格）。不在 ownerCell 的一律删除。
/// - `ULS_LiftBlocker`：只允许存在于“任一升降中的控制器声明的 blockerCell”。否则一律删除。
///
/// 触发时机：`FinalizeInit()`（读档/进图完成后）执行一次。
public class ULS_HiddenThingCleanupMapComponent : MapComponent
{
    public ULS_HiddenThingCleanupMapComponent(Map map) : base(map)
    {
    }

    public override void FinalizeInit()
    {
        base.FinalizeInit();
        CleanupHiddenThings();
    }

    private void CleanupHiddenThings()
    {
        Map map = this.map;
        if (map == null)
        {
            return;
        }

        // 1) 收集合法 ownerCell（代理允许存在的格）
        HashSet<IntVec3> flickOwnerCells = new HashSet<IntVec3>();
        HashSet<IntVec3> activeLiftBlockerCells = new HashSet<IntVec3>();

        // 控制器：ownerCell = controller.Position；升降中时声明 blockerCell
        List<Thing> allThings = map.listerThings?.AllThings;
        if (allThings != null)
        {
            for (int i = 0; i < allThings.Count; i++)
            {
                if (allThings[i] is not Building_WallController controller || controller.Destroyed || !controller.Spawned)
                {
                    continue;
                }

                flickOwnerCells.Add(controller.Position);
                if (controller.TryGetActiveLiftBlockerCell(out IntVec3 blockerCell) && blockerCell.InBounds(map))
                {
                    activeLiftBlockerCells.Add(blockerCell);
                }
            }
        }

        // 控制台：ownerCell = console.Position（仅玩家派系）
        ThingDef consoleDef = DefDatabase<ThingDef>.GetNamedSilentFail("ULS_LiftConsole");
        if (consoleDef != null)
        {
            List<Thing> consoles = map.listerThings.ThingsOfDef(consoleDef);
            if (consoles != null)
            {
                for (int i = 0; i < consoles.Count; i++)
                {
                    if (consoles[i] is ThingWithComps console && console.Spawned && !console.Destroyed && console.Faction == Faction.OfPlayer)
                    {
                        flickOwnerCells.Add(console.Position);
                    }
                }
            }
        }

        // 2) 清理错位/孤儿 FlickProxy
        int removedFlickProxy = 0;
        if (ULS_ThingDefOf.ULS_FlickProxy != null)
        {
            List<Thing> proxiesRaw = map.listerThings.ThingsOfDef(ULS_ThingDefOf.ULS_FlickProxy);
            if (proxiesRaw != null && proxiesRaw.Count > 0)
            {
                List<Thing> proxies = new List<Thing>(proxiesRaw);
                // 开发测试阶段：强制唯一性。
                // 同一 ownerCell（控制器格/控制台格）最多只允许 1 个代理；多余的一律删除，避免历史堆积继续污染后续逻辑。
                Dictionary<IntVec3, Thing> keptProxyByCell = new Dictionary<IntVec3, Thing>();
                for (int i = 0; i < proxies.Count; i++)
                {
                    Thing proxy = proxies[i];
                    if (proxy == null || proxy.Destroyed || !proxy.Spawned)
                    {
                        continue;
                    }

                    if (!flickOwnerCells.Contains(proxy.Position))
                    {
                        proxy.Destroy();
                        removedFlickProxy++;
                        continue;
                    }

                    // 同格去重：保留第一个，删除其余。
                    if (keptProxyByCell.TryGetValue(proxy.Position, out Thing kept) && kept != null && !kept.Destroyed)
                    {
                        proxy.Destroy();
                        removedFlickProxy++;
                    }
                    else
                    {
                        keptProxyByCell[proxy.Position] = proxy;
                    }
                }
            }
        }

        // 3) 清理孤儿 LiftBlocker
        int removedLiftBlocker = 0;
        if (ULS_ThingDefOf.ULS_LiftBlocker != null)
        {
            List<Thing> blockersRaw = map.listerThings.ThingsOfDef(ULS_ThingDefOf.ULS_LiftBlocker);
            if (blockersRaw != null && blockersRaw.Count > 0)
            {
                List<Thing> blockers = new List<Thing>(blockersRaw);
                for (int i = 0; i < blockers.Count; i++)
                {
                    Thing blocker = blockers[i];
                    if (blocker == null || blocker.Destroyed || !blocker.Spawned)
                    {
                        continue;
                    }

                    if (!activeLiftBlockerCells.Contains(blocker.Position))
                    {
                        blocker.Destroy();
                        removedLiftBlocker++;
                    }
                }
            }
        }

        if (removedFlickProxy > 0 || removedLiftBlocker > 0)
        {
            Log.Warning(
                $"[ULS] Hidden thing cleanup executed. map={map} removedFlickProxy={removedFlickProxy} removedLiftBlocker={removedLiftBlocker}");
        }
    }
}
