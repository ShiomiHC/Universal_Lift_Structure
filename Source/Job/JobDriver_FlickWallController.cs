
using Verse.AI;

namespace Universal_Lift_Structure
{
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

                // 如果是控制器，必须有 Designation
                if (thing is Building_WallController)
                {
                    if (Map.designationManager.DesignationOn(thing, ULS_DesignationDefOf.ULS_FlickLiftStructure) ==
                        null)
                    {
                        return true;
                    }
                }
                // 如果是控制台，不强制检查 Designation（因为全局队列任务不需要控制台有标记）
                // 而是检查是否仍有待处理请求（防止任务在该 Pawn 走路过程中被解决了）
                else if (thing.TryGetComp<CompLiftConsole>() is { HasPendingRequests: false })
                {
                    return true;
                }

                return false;
            });

            // 断电中断检查：若启用了电力需求且目标断电，则中断工作
            this.FailOn(delegate
            {
                UniversalLiftStructureSettings settings = UniversalLiftStructureMod.Settings;
                if (settings == null || !settings.enableLiftPower)
                {
                    return false; // 电力功能未启用，不检查
                }

                Thing thing = TargetA.Thing;
                CompPowerTrader powerComp = thing?.TryGetComp<CompPowerTrader>();
                if (powerComp is { PowerOn: false })
                {
                    return true; // 断电，中断工作
                }

                return false;
            });

            yield return Toils_Goto.GotoThing(TargetIndex.A, PathEndMode.Touch);

            yield return Toils_General.Wait(15) // 短暂等待模拟操作
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

                    // 移除 designations (虽然 controllers/console 内部可能已经移除了，这里再次确保)
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
}
