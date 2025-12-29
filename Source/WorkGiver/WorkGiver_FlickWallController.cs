using Verse.AI;

namespace Universal_Lift_Structure
{
    public class WorkGiver_FlickLiftStructure : WorkGiver_Scanner
    {
        public override IEnumerable<Thing> PotentialWorkThingsGlobal(Pawn pawn)
        {
            // 1. 返回所有带有 Designation 的物体（主要是控制器，在 Manual 模式或作为视觉提示）
            foreach (Designation des in pawn.Map.designationManager.SpawnedDesignationsOfDef(ULS_DesignationDefOf
                         .ULS_FlickLiftStructure))
            {
                yield return des.target.Thing;
            }

            // 2. 如果存在全局升降请求，则所有控制台也是潜在的工作目标
            // 只有当全局队列有任务时才扫描控制台，节省性能
            var mapComp = pawn.Map.GetComponent<ULS_LiftRequestMapComponent>();
            if (mapComp is { HasPendingRequests: true })
            {
                // 查找所有 Console 定义的物品
                // 注意：这里假设 ULS_LiftConsole 是控制台的 defName，最好用 DefDatabase 获取或者缓存
                ThingDef consoleDef = ULS_ThingDefOf.ULS_LiftConsole;
                if (consoleDef != null)
                {
                    foreach (Thing t in pawn.Map.listerThings.ThingsOfDef(consoleDef))
                    {
                        yield return t;
                    }
                }
            }
        }

        public override bool HasJobOnThing(Pawn pawn, Thing t, bool forced = false)
        {
            // 检查设置
            UniversalLiftStructureSettings settings = UniversalLiftStructureMod.Settings;
            bool checkPower = settings is { enableLiftPower: true };
            LiftControlMode controlMode = settings?.liftControlMode ?? LiftControlMode.Remote;

            // 情况 A: 目标是控制器
            if (t is Building_WallController controller)
            {
                // 如果是 Console/Remote 模式，Pawn 无法直接操作控制器（必须通过控制台）
                // 即使控制器上有 Designation (作为视觉提示)，也不应生成 Job
                if (controlMode != LiftControlMode.Manual)
                {
                    return false;
                }

                // Manual 模式下，必须因为有 Designation
                if (pawn.Map.designationManager.DesignationOn(t, ULS_DesignationDefOf.ULS_FlickLiftStructure) == null)
                {
                    return false;
                }

                if (!pawn.CanReserve(t, 1, -1, null, forced))
                {
                    return false;
                }

                // 优先检查能耗组件
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

            // 情况 B: 目标是控制台
            // 优化：仅获取一次 CompLiftConsole
            if (t.TryGetComp<CompLiftConsole>() is { } console)
            {
                // Console 模式下才允许操作控制台
                // 虽然理论上 Remote 模式也可能不允许，但这里主要区分手动和控制台

                // 检查是否有待处理的请求 (HasPendingRequests 已经改为查询全局队列)
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
                    // 使用缓存的 PowerTraderComp
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
}