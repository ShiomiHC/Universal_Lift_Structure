namespace Universal_Lift_Structure;

public class CompProperties_LiftConsole : CompProperties
{
    public CompProperties_LiftConsole()
    {
        compClass = typeof(CompLiftConsole);
    }
}

public class CompLiftConsole : ThingComp
{
    private CompPowerTrader cachedPowerComp;

    public CompProperties_LiftConsole Props => (CompProperties_LiftConsole)props;

    // 查询全局队列是否有待处理请求（用于 UI 提示等）
    public bool HasPendingRequests
    {
        get
        {
            if (parent?.Map == null) return false;
            var mapComp = parent.Map.GetComponent<ULS_LiftRequestMapComponent>();
            return mapComp is { HasPendingRequests: true };
        }
    }

    public CompPowerTrader PowerTraderComp
    {
        get
        {
            cachedPowerComp ??= parent.GetComp<CompPowerTrader>();

            return cachedPowerComp;
        }
    }

    // 控制台被"flick"时，执行全局队列中的所有请求
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

        // 使用 PooledList 减少内存分配
        using var _ = new PooledList<ULS_LiftRequest>(out var requestsToExecute);

        // 从全局队列取出所有请求
        mapComp.DequeueAllRequests(requestsToExecute);

        // 执行所有请求
        foreach (var request in requestsToExecute)
        {
            if (request.controller == null || request.controller.Destroyed || !request.controller.Spawned)
            {
                continue;
            }

            if (request.type == ULS_LiftRequestType.RaiseGroup)
            {
                request.controller.GizmoRaiseGroup();
            }
            else
            {
                request.controller.GizmoLowerGroup(request.startCell);
            }

            // 执行完毕后，清除控制器的期望状态（视为请求已完成）
            // 这也会触发 UpdateLiftDesignation，移除视觉标记
            request.controller.CancelLiftAction();
        }
    }
}
