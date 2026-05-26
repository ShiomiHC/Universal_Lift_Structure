using Verse.AI;

namespace Universal_Lift_Structure;

public class JobDriver_FlickLiftStructure : JobDriver
{
    public override bool TryMakePreToilReservations(bool errorOnFailed)
    {
        return pawn.Reserve(TargetA, job, 1, -1, null, errorOnFailed);
    }

    protected override IEnumerable<Toil> MakeNewToils()
    {
        this.FailOnDespawnedOrNull(TargetIndex.A);

        this.FailOn(delegate
        {
            Thing thing = TargetA.Thing;

            if (thing is Building_WallController)
            {
                if (Map.designationManager.DesignationOn(thing, ULS_DesignationDefOf.ULS_FlickLiftStructure) ==
                    null)
                {
                    return true;
                }
            }
            // 控制台不检查 Designation，而是检查是否仍有待处理请求（防止任务在走路途中被解决）
            else if (thing.TryGetComp<CompLiftConsole>() is { HasPendingRequests: false })
            {
                return true;
            }

            return false;
        });

        this.FailOn(delegate
        {
            UniversalLiftStructureSettings settings = UniversalLiftStructureMod.Settings;
            if (settings == null || !settings.enableLiftPower)
            {
                return false;
            }

            Thing thing = TargetA.Thing;
            CompPowerTrader powerComp = thing?.TryGetComp<CompPowerTrader>();
            if (powerComp is { PowerOn: false })
            {
                return true;
            }

            return false;
        });

        yield return Toils_Goto.GotoThing(TargetIndex.A, PathEndMode.Touch);

        yield return Toils_General.Wait(15)
            .FailOnCannotTouch(TargetIndex.A, PathEndMode.Touch)
            .WithProgressBarToilDelay(TargetIndex.A);

        Toil finalize = new Toil
        {
            initAction = delegate
            {
                Thing thing = TargetA.Thing;
                Pawn actor = pawn;

                if (thing is Building_WallController controller)
                {
                    controller.Notify_FlickedBy(actor);
                }
                else if (thing.TryGetComp<CompLiftConsole>() is { } console)
                {
                    console.NotifyFlicked();
                }

                // controller/console 内部逻辑可能已移除 designation，此处保险再清一次防止残留
                Designation des =
                    Map.designationManager.DesignationOn(thing, ULS_DesignationDefOf.ULS_FlickLiftStructure);
                if (des != null)
                {
                    des.Delete();
                }
            },
            defaultCompleteMode = ToilCompleteMode.Instant
        };
        yield return finalize;
    }
}