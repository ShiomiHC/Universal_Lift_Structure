using Verse.AI;

namespace Universal_Lift_Structure;

// 扫描地图上的控制器和控制台，为符合条件的 Pawn 生成 JobDriver_FlickLiftStructure
public class WorkGiver_FlickLiftStructure : WorkGiver_Scanner
{
    public override IEnumerable<Thing> PotentialWorkThingsGlobal(Pawn pawn)
    {
        // Manual 模式下带 Designation 的控制器
        foreach (Designation des in pawn.Map.designationManager.SpawnedDesignationsOfDef(ULS_DesignationDefOf
                     .ULS_FlickLiftStructure))
        {
            yield return des.target.Thing;
        }

        // 有全局升降请求时，控制台也是潜在目标
        var mapComp = pawn.Map.GetComponent<ULS_LiftRequestMapComponent>();
        if (mapComp is { HasPendingRequests: true })
        {
            ThingDef consoleDef = ULS_ThingDefOf.ULS_LiftConsole;
            if (consoleDef == null) yield break;

            foreach (Thing t in pawn.Map.listerThings.ThingsOfDef(consoleDef))
            {
                yield return t;
            }
        }
    }

    public override bool HasJobOnThing(Pawn pawn, Thing t, bool forced = false)
    {
        UniversalLiftStructureSettings settings = UniversalLiftStructureMod.Settings;
        bool checkPower = settings is { enableLiftPower: true };
        LiftControlMode controlMode = settings?.liftControlMode ?? LiftControlMode.Remote;

        if (t is Building_WallController controller)
        {
            // 非 Manual 模式不允许直接操作控制器，即使存在 Designation
            if (controlMode != LiftControlMode.Manual)
            {
                return false;
            }

            if (pawn.Map.designationManager.DesignationOn(t, ULS_DesignationDefOf.ULS_FlickLiftStructure) == null)
            {
                return false;
            }

            if (!pawn.CanReserve(t, 1, -1, null, forced))
            {
                return false;
            }

            if (checkPower)
            {
                CompPowerTrader powerComp = controller.PowerTraderComp;
                if (powerComp is { PowerOn: false })
                {
                    return false;
                }
            }

            return controller.LiftActionPending;
        }

        if (t.TryGetComp<CompLiftConsole>() is { } console)
        {
            if (!console.HasPendingRequests)
            {
                return false;
            }

            if (!pawn.CanReserve(t, 1, -1, null, forced))
            {
                return false;
            }

            if (checkPower)
            {
                CompPowerTrader powerComp = console.PowerTraderComp;
                if (powerComp is { PowerOn: false })
                {
                    return false;
                }
            }

            return true;
        }

        return false;
    }

    public override Job JobOnThing(Pawn pawn, Thing t, bool forced = false)
    {
        return JobMaker.MakeJob(ULS_JobDefOf.ULS_FlickLiftStructure, t);
    }
}
