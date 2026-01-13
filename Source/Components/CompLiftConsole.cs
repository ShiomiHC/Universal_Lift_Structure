namespace Universal_Lift_Structure;

// ============================================================
// 【控制台属性定义】
// ============================================================
// 升降控制台组件的属性定义
//
// 作用：
// 1. 定义组件的类类型
// 2. 可以在 XML 中配置（如果有参数）
// ============================================================
public class CompProperties_LiftConsole : CompProperties
{
    public CompProperties_LiftConsole()
    {
        compClass = typeof(CompLiftConsole);
    }
}

// ============================================================
// 【升降控制台组件】
// ============================================================
// 升降控制台组件 (Lift Console)
//
// 功能描述：
// 1. 作为集中控制点，处理整个地图范围内的升降请求。
// 2. 提供与 PowerTrader 的集成，受电力状态影响（断电无法操作）。
// 3. 响应 Pawn 的操作（Flick job），当被操作时，一次性执行全局队列中的所有待处理请求。
// 4. 充当 WorkGiver 的扫描目标，当全局有任务时，允许 Pawn 前来操作。
// ============================================================
public class CompLiftConsole : ThingComp
{
    // 缓存电力组件引用，避免频繁 GetComponent
    private CompPowerTrader cachedPowerComp;

    public CompProperties_LiftConsole Props => (CompProperties_LiftConsole)props;

    // ============================================================
    // 【查询待处理请求】
    // ============================================================
    // 查询全局队列是否有待处理请求
    //
    // 用途：
    // - WorkGiver 判断是否需要生成任务
    // - UI 提示（如感叹号或覆盖层）
    //
    // 实现原理：
    // 这里的状态其实是代理了 MapComponent 中的全局状态。控制台本身不存储请求，
    // 而是查看 ULS_LiftRequestMapComponent 是否有积压。
    // ============================================================
    public bool HasPendingRequests
    {
        get
        {
            if (parent?.Map == null) return false;
            var mapComp = parent.Map.GetComponent<ULS_LiftRequestMapComponent>();
            return mapComp is { HasPendingRequests: true };
        }
    }

    // ============================================================
    // 【获取电力组件】
    // ============================================================
    // 获取电力组件（带缓存）
    // ============================================================
    public CompPowerTrader PowerTraderComp
    {
        get
        {
            cachedPowerComp ??= parent.GetComp<CompPowerTrader>();

            return cachedPowerComp;
        }
    }

    // ============================================================
    // 【Flick 操作通知】
    // ============================================================
    // 通知：控制台被 Pawn 操作（Flicked）
    //
    // 触发时机：
    // JobDriver_FlickLiftStructure 的 Toil 完成时调用。
    //
    // 行为：
    // 立即执行全局队列中的所有请求，清理积压的任务。
    // ============================================================
    public void NotifyFlicked()
    {
        if (parent?.Map == null)
        {
            return;
        }

        var mapComp = parent.Map.GetComponent<ULS_LiftRequestMapComponent>();
        if (mapComp == null)
        {
            return;
        }

        // 使用 PooledList 减少内存分配，因为此操作可能频繁发生
        using var _ = new PooledList<ULS_LiftRequest>(out var requestsToExecute);

        // 从全局队列取出所有等待执行的请求
        // 注意：这里是一次性取出所有，意味着一次操作处理全部积压工作
        mapComp.DequeueAllRequests(requestsToExecute);

        // 逐个执行取出请求
        foreach (var request in requestsToExecute)
        {
            // 1. 校验控制器状态，防止已被销毁或失效
            if (request.controller == null || request.controller.Destroyed || !request.controller.Spawned)
            {
                continue;
            }

            // 2. 根据请求类型调用控制器的相应方法（执行升起或降下）
            if (request.type == ULS_LiftRequestType.RaiseGroup)
            {
                request.controller.GizmoRaiseGroup();
            }
            else
            {
                request.controller.GizmoLowerGroup(request.startCell);
            }

            // 3. 执行完毕后，清除控制器的期望状态（LiftActionPending）
            // 这表示由于这次操作，该控制器的挂起请求已被满足。
            // 这一步至关重要，因为它会触发控制器内部的状态重置，并移除 UpdateLiftDesignation 中的视觉标记。
            request.controller.CancelLiftAction();
        }
    }
}
