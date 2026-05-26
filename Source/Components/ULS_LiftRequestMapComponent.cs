namespace Universal_Lift_Structure;

public class ULS_LiftRequestMapComponent : MapComponent
{
    // 全局待处理请求队列（会被序列化）
    private List<ULS_LiftRequest> globalPendingRequests = new List<ULS_LiftRequest>();

    public ULS_LiftRequestMapComponent(Map map) : base(map)
    {
    }

    public bool HasPendingRequests => globalPendingRequests.Count > 0;

    // 添加请求并自动去重：保证每个控制器最多只有一个待处理请求
    public void EnqueueRequest(ULS_LiftRequest request)
    {
        if (request == null || request.controller == null)
        {
            return;
        }

        globalPendingRequests ??= new List<ULS_LiftRequest>();

        for (int i = globalPendingRequests.Count - 1; i >= 0; i--)
        {
            if (globalPendingRequests[i].controller == request.controller)
            {
                globalPendingRequests.RemoveAt(i);
            }
        }

        globalPendingRequests.Add(request);
        // 不在这里调用 UpdateLiftDesignation()，调用者本身可能在其中，会导致循环调用
    }

    public void DequeueAllRequests(List<ULS_LiftRequest> outList)
    {
        if (outList == null || globalPendingRequests == null)
        {
            return;
        }

        outList.Clear();
        outList.AddRange(globalPendingRequests);
        globalPendingRequests.Clear();
    }

    // 从 Console 模式切换到 Interactive 模式时，丢弃所有未处理请求
    public void ClearAllRequests()
    {
        globalPendingRequests?.Clear();
    }

    public void RemoveRequestsForController(Building_WallController controller)
    {
        if (globalPendingRequests == null || controller == null)
        {
            return;
        }

        for (int i = globalPendingRequests.Count - 1; i >= 0; i--)
        {
            if (globalPendingRequests[i].controller == controller)
            {
                globalPendingRequests.RemoveAt(i);
            }
        }

        // 不在这里调用 UpdateLiftDesignation()，调用者本身可能在其中
    }

    public bool HasRequestForController(Building_WallController controller)
    {
        if (globalPendingRequests == null || controller == null)
        {
            return false;
        }

        foreach (var request in globalPendingRequests)
        {
            if (request.controller == controller)
            {
                return true;
            }
        }

        return false;
    }

    public ULS_LiftRequest GetRequestForController(Building_WallController controller)
    {
        if (globalPendingRequests == null || controller == null)
        {
            return null;
        }

        foreach (var request in globalPendingRequests)
        {
            if (request.controller == controller)
            {
                return request;
            }
        }

        return null;
    }

    public override void ExposeData()
    {
        base.ExposeData();
        Scribe_Collections.Look(ref globalPendingRequests, "globalPendingRequests", LookMode.Deep);

        if (Scribe.mode == LoadSaveMode.PostLoadInit && globalPendingRequests == null)
        {
            globalPendingRequests = new List<ULS_LiftRequest>();
        }
    }
}
