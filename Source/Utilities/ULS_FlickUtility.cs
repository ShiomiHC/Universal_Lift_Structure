namespace Universal_Lift_Structure;





public static class ULS_FlickUtility
{
    private static readonly AccessTools.FieldRef<CompFlickable, bool> WantSwitchOnRef =
        AccessTools.FieldRefAccess<CompFlickable, bool>("wantSwitchOn");


    
    
    
    public static void RequestPulseFlick(ThingWithComps thing)
    {
        if (thing == null)
        {
            return;
        }

        CompFlickable flickable = thing.GetComp<CompFlickable>();
        if (flickable == null)
        {
            Log.Error($"[ULS] RequestPulseFlick failed: target has no CompFlickable. target={thing}");
            return;
        }

        WantSwitchOnRef(flickable) = !flickable.SwitchIsOn;
        FlickUtility.UpdateFlickDesignation(thing);
    }


    
    
    
    
    public static ULS_FlickTrigger GetOrCreateFlickProxyTriggerAt(Map map, IntVec3 ownerCell)
    {
        if (map == null || !ownerCell.IsValid || !ownerCell.InBounds(map))
        {
            return null;
        }


        List<Thing> list = map.thingGrid.ThingsListAt(ownerCell);
        foreach (var t in list)
        {
            if (t is ThingWithComps existing && existing.def == ULS_ThingDefOf.ULS_FlickProxy)
            {
                return existing.GetComp<ULS_FlickTrigger>();
            }
        }

        ThingWithComps proxy = ThingMaker.MakeThing(ULS_ThingDefOf.ULS_FlickProxy) as ThingWithComps;
        if (proxy == null)
        {
            return null;
        }


        GenSpawn.Spawn(proxy, ownerCell, map, Rot4.North);
        return proxy.GetComp<ULS_FlickTrigger>();
    }
}
