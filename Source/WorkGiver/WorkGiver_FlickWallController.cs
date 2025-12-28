using System.Collections.Generic;
using RimWorld;
using Verse;
using Verse.AI;

namespace Universal_Lift_Structure
{
    public class WorkGiver_FlickLiftStructure : WorkGiver_Scanner
    {
        public override IEnumerable<Thing> PotentialWorkThingsGlobal(Pawn pawn)
        {
            foreach (Designation des in pawn.Map.designationManager.SpawnedDesignationsOfDef(ULS_DesignationDefOf
                         .ULS_FlickLiftStructure))
            {
                yield return des.target.Thing;
            }
        }

        public override bool HasJobOnThing(Pawn pawn, Thing t, bool forced = false)
        {
            if (pawn.Map.designationManager.DesignationOn(t, ULS_DesignationDefOf.ULS_FlickLiftStructure) == null)
            {
                return false;
            }

            if (!pawn.CanReserve(t, 1, -1, null, forced))
            {
                return false;
            }

            // 电力检查：如果启用了电力需求且目标断电，则不分配 job（避免循环失败）
            UniversalLiftStructureSettings settings = UniversalLiftStructureMod.Settings;
            if (settings is { enableLiftPower: true })
            {
                CompPowerTrader powerComp = t?.TryGetComp<CompPowerTrader>();
                if (powerComp != null && !powerComp.PowerOn)
                {
                    return false;
                }
            }

            // 检查目标是否是我们的有效目标
            if (t is Building_WallController controller)
            {
                // 只有当有挂起的升降操作时才处理
                // 注意：我们通过 DesignationDefOf.Flick 来判断意图，
                // 但为了严谨，我们应该检查 controller.pendingLiftAction
                // 然而，Flick 指定也可能是用来开关电源的？
                // 原版 Building_WallController 只有 CompPowerTrader，开关电源也是 Flick。
                // 但我们的 JobDriver 会处理 “触发 Lift” 的逻辑。
                // 如果只是普通的 Flick Power，原版 WorkGiver_Flick 也会认领。
                // 为了避免冲突，我们需要确认：
                // 1. 如果是 Lift 操作，priority 应该比普通 flick 高（我们在 Def 里设置了 priorityInType 100）。
                // 2. JobDriver 必须能处理两种情况，或者我们只处理 Lift 情况。

                // 策略：如果 liftActionPending 为真，我们接管。
                return controller.LiftActionPending;
            }

            if (t.TryGetComp<CompLiftConsole>() is CompLiftConsole console)
            {
                return console.HasPendingRequests;
            }

            return false;
        }

        public override Job JobOnThing(Pawn pawn, Thing t, bool forced = false)
        {
            return JobMaker.MakeJob(ULS_JobDefOf.ULS_FlickLiftStructure, t);
        }
    }
}
