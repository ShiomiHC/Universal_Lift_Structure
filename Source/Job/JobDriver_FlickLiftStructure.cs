using Verse.AI;

namespace Universal_Lift_Structure;

// 负责执行升降操作的 JobDriver (Pawn 的具体工作流程)
// 包含走向目标 -> 等待(操作) -> 完成(触发事件) 的流程
public class JobDriver_FlickLiftStructure : JobDriver
{
    // 尝试在工作开始前预留目标
    public override bool TryMakePreToilReservations(bool errorOnFailed)
    {
        return pawn.Reserve(TargetA, job, 1, -1, null, errorOnFailed);
    }

    // 定义工作流程 (Toils)
    protected override IEnumerable<Toil> MakeNewToils()
    {
        // 1. 基本检查：如果目标消失或被销毁，则任务失败
        this.FailOnDespawnedOrNull(TargetIndex.A);

        // 2. 状态检查：如果目标不再满足操作条件，则任务失败
        this.FailOn(delegate
        {
            Thing thing = TargetA.Thing;

            // 分情况检查控制器和控制台
            if (thing is Building_WallController)
            {
                // 如果是控制器，必须仍有 Designation (表示玩家或系统仍希望操作它)
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

        // 3. 断电中断检查：若启用了电力需求且目标断电，则中断工作
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

        // 4. 走向目标
        yield return Toils_Goto.GotoThing(TargetIndex.A, PathEndMode.Touch);

        // 5. 执行操作（短暂等待模拟拨动开关）
        yield return Toils_General.Wait(15)
            .FailOnCannotTouch(TargetIndex.A, PathEndMode.Touch)
            .WithProgressBarToilDelay(TargetIndex.A);

        // 6. 完成操作
        Toil finalize = new Toil
        {
            initAction = delegate
            {
                Thing thing = TargetA.Thing;
                Pawn actor = pawn;

                // 触发具体的完成逻辑
                if (thing is Building_WallController controller)
                {
                    controller.Notify_FlickedBy(actor);
                }
                else if (thing.TryGetComp<CompLiftConsole>() is { } console)
                {
                    console.NotifyFlicked();
                }

                // 清理工作：移除 designations 
                // 虽然 controllers/console 内部逻辑（如 CancelLiftAction）可能已经移除了，
                // 但为了保险起见，这里再次尝试移除，防止残留
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