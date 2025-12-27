namespace Universal_Lift_Structure;

/// Properties 意图：为控制台/控制器提供“接收请求 + 触发 flick + flick 完成后执行请求”的能力。
public class CompProperties_ULS_FlickTrigger : CompProperties
{
    public CompProperties_ULS_FlickTrigger()
    {
        compClass = typeof(ULS_FlickTrigger);
    }
}


/// Comp 意图：维护一个请求
/// - Console 模式：请求队列挂在控制台上
/// - Manual 模式：请求队列挂在控制器本体上
public class ULS_FlickTrigger : ThingComp
{
    private List<ULS_LiftRequest> pendingRequests = new();

    // 标记意图：请求侧节流。队列从“空→非空”时才触发一次 flick designation，避免频繁点击反复 UpdateFlickDesignation。
    private bool flickRequested;

    /// 方法意图：确保“已有挂起请求”会触发一次 flick 派工，但避免重复触发。
    private void RequestPulseFlickIfNeeded()
    {
        if (flickRequested)
        {
            return;
        }

        flickRequested = true;
        ULS_FlickUtility.RequestPulseFlick(parent);
    }

    public override void PostExposeData()
    {
        base.PostExposeData();

        Scribe_Collections.Look(ref pendingRequests, "pendingRequests", LookMode.Deep);
        if (Scribe.mode is LoadSaveMode.PostLoadInit && pendingRequests is null)
        {
            pendingRequests = new();
        }
    }

    public override void PostSpawnSetup(bool respawningAfterLoad)
    {
        base.PostSpawnSetup(respawningAfterLoad);

        // 读档后若存在挂起请求：主动触发一次 designation，恢复原版 flick 派工链路。
        if (respawningAfterLoad && pendingRequests is { Count: > 0 })
        {
            RequestPulseFlickIfNeeded();
        }
    }


    /// 方法意图：向队列追加请求并触发一次 flick 派工。
    public void EnqueueRequest(ULS_LiftRequest request)
    {
        if (request is null)
        {
            return;
        }

        pendingRequests ??= new();

        // 合并策略：同一 controller “最后一次为准”。
        // 说明：当前交互下几乎不会出现 Raise/Lower 冲突，因此允许跨 type 直接覆盖。
        Building_WallController controller = request.controller;
        for (int i = pendingRequests.Count - 1; i >= 0; i--)
        {
            if (pendingRequests[i]?.controller == controller)
            {
                pendingRequests.RemoveAt(i);
            }
        }

        pendingRequests.Add(request);
        RequestPulseFlickIfNeeded();
    }

    public override void ReceiveCompSignal(string signal)
    {
        base.ReceiveCompSignal(signal);

        if (signal is not ("FlickedOn" or "FlickedOff"))
        {
            return;
        }

        TryExecuteAllRequests();
    }


    /// 方法意图：在一次 flick 完成时，尽可能“批量”执行当前队列内的全部请求。
    /// 说明：这会减少 flick job / designation 的数量，从而简化逻辑并降低频繁点击下的系统开销。
    private void TryExecuteAllRequests()
    {
        if (pendingRequests is not { Count: > 0 })
        {
            return;
        }

        // 为避免 List.RemoveAt(0) 的 O(n^2) 移位开销：复制快照后一次性清空。
        List<ULS_LiftRequest> requests = new(pendingRequests);
        pendingRequests.Clear();

        // 本次 flick 已完成：允许后续新请求再次触发下一次 flick。
        flickRequested = false;

        for (int i = 0; i < requests.Count; i++)
        {
            ULS_LiftRequest request = requests[i];

            Building_WallController controller = request?.controller;
            if (controller is null || controller.Destroyed || !controller.Spawned || controller.Map is null)
            {
                Messages.Message("ULS_LiftRequest_ControllerInvalid".Translate(), MessageTypeDefOf.RejectInput,
                    false);
                continue;
            }

            if (request.type == ULS_LiftRequestType.RaiseGroup)
            {
                controller.GizmoRaiseGroup();
            }
            else
            {
                controller.GizmoLowerGroup(request.startCell);
            }
        }

        // 若在执行过程中又产生了新的请求（例如玩家继续点击）：触发下一次 flick。
        if (pendingRequests.Count > 0)
        {
            RequestPulseFlickIfNeeded();
        }
    }
}