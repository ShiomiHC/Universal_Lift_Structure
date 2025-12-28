namespace Universal_Lift_Structure;



public partial class Building_WallController
{
    


    private ThingWithComps flickProxy;


    private const float ActiveLiftPower = 1000f; 


    private CompPowerTrader compPower;
    private float idlePowerConsumption;         
    private bool activePowerApplied;            




    private bool PowerFeatureEnabled => UniversalLiftStructureMod.Settings?.enableLiftPower ?? false;


    private CompPowerTrader PowerCompInternal
    {
        get
        {
            if (compPower == null)
            {
                compPower = GetComp<CompPowerTrader>();
                if (compPower != null)
                {
                    idlePowerConsumption = Mathf.Max(0f, compPower.Props.PowerConsumption);
                }
            }
            return compPower;
        }
    }


    private bool HasPowerComp => PowerCompInternal != null;


    private bool PowerOn
    {
        get
        {
            if (HasPowerComp)
            {
                return compPower.PowerOn;
            }
            return false;
        }
    }




    private void RefreshPowerCacheAndOutput()
    {
        CompPowerTrader comp = (compPower = GetComp<CompPowerTrader>());
        idlePowerConsumption = (comp != null) ? Mathf.Max(0f, comp.Props.PowerConsumption) : 0f;

        if (PowerFeatureEnabled && comp != null)
        {
            ApplyActivePowerInternal(InLiftProcess);
        }
    }


    
    private void ApplyActivePowerInternal(bool active)
    {
        if (PowerFeatureEnabled)
        {
            CompPowerTrader comp = PowerCompInternal;
            if (comp != null)
            {
                float idlePower = idlePowerConsumption;
                comp.PowerOutput = active
                    ? (0f - (idlePower + ActiveLiftPower))  
                    : (0f - idlePower);                     
                activePowerApplied = active;
            }
        }
    }


    private void EnsureIdlePowerIfFeatureDisabled()
    {
        if (!PowerFeatureEnabled && activePowerApplied && HasPowerComp)
        {
            compPower.PowerOutput = 0f - idlePowerConsumption;
            activePowerApplied = false;
        }
    }


    private bool IsReadyForLiftPower()
    {
        if (!PowerFeatureEnabled)
        {
            return true; 
        }

        if (HasPowerComp)
        {
            return PowerOn; 
        }

        return false;
    }


    private void HandlePowerLossDuringLift()
    {
        LiftProcessState previousState = liftProcessState;


        ClearLiftProcessAndRemoveBlocker();

        Map map = Map;
        if (map != null && previousState == LiftProcessState.Lowering && HasStored)
        {
            
            TryRaiseNoMessage(map);
        }

        MessageReject("ULS_PowerLost", this);
    }




    private ThingWithComps EnsureFlickProxy()
    {
        Map map = Map;
        IntVec3 pos = Position;
        if (map == null || !pos.IsValid)
        {
            return null;
        }


        if (flickProxy is { Destroyed: false, Spawned: true })
        {
            return flickProxy;
        }


        List<Thing> things = map.thingGrid.ThingsListAt(pos);
        ThingWithComps firstProxy = null;
        foreach (var t in things)
        {
            if (t.def == ULS_ThingDefOf.ULS_FlickProxy)
            {
                if (firstProxy == null)
                {
                    firstProxy = t as ThingWithComps;
                    continue;
                }

                t.Destroy();
            }
        }

        if (firstProxy is { Destroyed: false })
        {
            flickProxy = firstProxy;
            return flickProxy;
        }


        ULS_FlickUtility.GetOrCreateFlickProxyTriggerAt(map, pos);


        List<Thing> thingsAfterCreate = map.thingGrid.ThingsListAt(pos);
        foreach (var t in thingsAfterCreate)
        {
            if (t.def == ULS_ThingDefOf.ULS_FlickProxy)
            {
                flickProxy = t as ThingWithComps;
                return flickProxy;
            }
        }

        return null;
    }


    internal ULS_FlickTrigger GetProxyFlickTrigger()
    {
        return EnsureFlickProxy()?.GetComp<ULS_FlickTrigger>();
    }
}
