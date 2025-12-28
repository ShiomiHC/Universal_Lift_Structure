namespace Universal_Lift_Structure;

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
        Map mapInstance = map;
        if (mapInstance == null)
        {
            return;
        }


        HashSet<IntVec3> flickOwnerCells = new HashSet<IntVec3>();
        HashSet<IntVec3> activeLiftBlockerCells = new HashSet<IntVec3>();


        List<Thing> allThings = mapInstance.listerThings?.AllThings;
        if (allThings != null)
        {
            foreach (var t in allThings)
            {
                if (t is not Building_WallController controller || controller.Destroyed || !controller.Spawned)
                {
                    continue;
                }

                flickOwnerCells.Add(controller.Position);
                if (controller.TryGetActiveLiftBlockerCell(out IntVec3 blockerCell) &&
                    blockerCell.InBounds(mapInstance))
                {
                    activeLiftBlockerCells.Add(blockerCell);
                }
            }
        }


        ThingDef consoleDef = DefDatabase<ThingDef>.GetNamedSilentFail("ULS_LiftConsole");
        if (consoleDef != null)
        {
            List<Thing> consoles = mapInstance.listerThings?.ThingsOfDef(consoleDef);
            if (consoles != null)
            {
                foreach (var t in consoles)
                {
                    if (t is ThingWithComps { Spawned: true, Destroyed: false } console &&
                        console.Faction == Faction.OfPlayer)
                    {
                        flickOwnerCells.Add(console.Position);
                    }
                }
            }
        }


        int removedFlickProxy = 0;
        if (ULS_ThingDefOf.ULS_FlickProxy != null)
        {
            List<Thing> proxiesRaw = mapInstance.listerThings?.ThingsOfDef(ULS_ThingDefOf.ULS_FlickProxy);
            if (proxiesRaw is { Count: > 0 })
            {
                List<Thing> proxies = new List<Thing>(proxiesRaw);


                Dictionary<IntVec3, Thing> keptProxyByCell = new Dictionary<IntVec3, Thing>();
                foreach (var proxy in proxies)
                {
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


                    if (keptProxyByCell.TryGetValue(proxy.Position, out Thing kept) && kept is { Destroyed: false })
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


        int removedLiftBlocker = 0;
        if (ULS_ThingDefOf.ULS_LiftBlocker != null)
        {
            List<Thing> blockersRaw = mapInstance.listerThings?.ThingsOfDef(ULS_ThingDefOf.ULS_LiftBlocker);
            if (blockersRaw is { Count: > 0 })
            {
                List<Thing> blockers = new List<Thing>(blockersRaw);
                foreach (var blocker in blockers)
                {
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
                $"[ULS] Hidden thing cleanup executed. map={mapInstance} removedFlickProxy={removedFlickProxy} removedLiftBlocker={removedLiftBlocker}");
        }
    }
}