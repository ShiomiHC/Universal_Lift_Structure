using Verse.AI;

namespace Universal_Lift_Structure;

// 工作分派器：负责生成操作升降结构的任务
// 扫描地图上的控制器和控制台，为符合条件的 Pawn 生成 JobDriver_FlickLiftStructure
// 继承自 WorkGiver_Scanner 以支持全局扫描模式
public class WorkGiver_FlickLiftStructure : WorkGiver_Scanner
{
    // 全局扫描潜在的工作目标
    // 这里不仅返回带有 Designation 的控制器，也返回控制台（如果有全局请求）
    public override IEnumerable<Thing> PotentialWorkThingsGlobal(Pawn pawn)
    {
        // 1. 返回所有带有 ULS_FlickLiftStructure 指定（Designation）的物体
        // 这些通常是处于 Manual 模式的控制器，或者玩家手动标记的视觉提示
        foreach (Designation des in pawn.Map.designationManager.SpawnedDesignationsOfDef(ULS_DesignationDefOf
                     .ULS_FlickLiftStructure))
        {
            yield return des.target.Thing;
        }

        // 2. 如果存在全局升降请求，则所有控制台也是潜在的工作目标
        // optimization: 只有当全局队列有任务时才去扫描控制台，节省性能
        var mapComp = pawn.Map.GetComponent<ULS_LiftRequestMapComponent>();
        if (mapComp is { HasPendingRequests: true })
        {
            // 查找所有 Console 定义的物品
            // 注意：ULS_LiftConsole 是控制台的 ThingDef
            ThingDef consoleDef = ULS_ThingDefOf.ULS_LiftConsole;
            if (consoleDef == null) yield break;

            // 使用 listerThings 快速获取所有控制台实例
            foreach (Thing t in pawn.Map.listerThings.ThingsOfDef(consoleDef))
            {
                yield return t;
            }
        }
    }

    // 判断 Pawn 是否可以对特定目标执行工作
    // 逻辑根据目标类型（控制器 vs 控制台）有所不同
    public override bool HasJobOnThing(Pawn pawn, Thing t, bool forced = false)
    {
        // 获取全局设置
        UniversalLiftStructureSettings settings = UniversalLiftStructureMod.Settings;
        bool checkPower = settings is { enableLiftPower: true };
        LiftControlMode controlMode = settings?.liftControlMode ?? LiftControlMode.Remote;

        // 情况 A: 目标是控制器 (Building_WallController)
        if (t is Building_WallController controller)
        {
            // 规则检查：如果不是 Manual 模式，Pawn 无法直接操作控制器
            // 此时必须通过控制台进行操作
            // 即使控制器上有 Designation (可能是残留的或是作为视觉标记)，也不应生成 Job
            if (controlMode != LiftControlMode.Manual)
            {
                return false;
            }

            // 在 Manual 模式下，必须有 Designation 玩家才允许操作
            if (pawn.Map.designationManager.DesignationOn(t, ULS_DesignationDefOf.ULS_FlickLiftStructure) == null)
            {
                return false;
            }

            // 检查是否可被预留
            if (!pawn.CanReserve(t, 1, -1, null, forced))
            {
                return false;
            }

            // 检查电力（如果启用）
            // 注意：控制器没电时无法被操作
            if (checkPower)
            {
                CompPowerTrader powerComp = controller.PowerTraderComp;
                if (powerComp is { PowerOn: false })
                {
                    return false;
                }
            }

            // 最后确认控制器确实有挂起的动作请求
            return controller.LiftActionPending;
        }

        // 情况 B: 目标是控制台 (CompLiftConsole)
        // 尝试获取控制台组件
        if (t.TryGetComp<CompLiftConsole>() is { } console)
        {
            // 逻辑检查：Console 模式下才允许操作控制台
            // Remote 模式通常自动化，Manual 模式直接操作控制器
            // 但这里主要区分 Manual 和非 Manual。如果通过 HasPendingRequests 检查，说明有任务。

            // 检查是否有待处理的请求 ( HasPendingRequests 代理了全局队列状态 )
            if (!console.HasPendingRequests)
            {
                return false;
            }

            // 检查是否可被预留
            if (!pawn.CanReserve(t, 1, -1, null, forced))
            {
                return false;
            }

            // 检查电力（如果启用）
            // 注意：控制台没电时无法执行指令
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

    // 创建具体的工作 Job
    public override Job JobOnThing(Pawn pawn, Thing t, bool forced = false)
    {
        return JobMaker.MakeJob(ULS_JobDefOf.ULS_FlickLiftStructure, t);
    }
}