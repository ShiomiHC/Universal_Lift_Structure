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
    // 避免频繁 GetComponent
    private CompPowerTrader cachedPowerComp;

    public CompProperties_LiftConsole Props => (CompProperties_LiftConsole)props;

    // 代理 ULS_LiftRequestMapComponent 的全局积压状态；控制台本身不存储请求
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

    // 由 JobDriver_FlickLiftStructure 的 Toil 完成时调用，一次性执行全局队列中所有待处理请求
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

        // PooledList 减少内存分配
        using var _ = new PooledList<ULS_LiftRequest>(out var requestsToExecute);

        // 一次性取出所有积压请求
        mapComp.DequeueAllRequests(requestsToExecute);

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

            // CancelLiftAction 触发控制器内部状态重置，并移除 UpdateLiftDesignation 中的视觉标记
            request.controller.CancelLiftAction();
        }
    }
}
