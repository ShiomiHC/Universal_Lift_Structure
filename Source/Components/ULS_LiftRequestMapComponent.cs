namespace Universal_Lift_Structure;

// 全局升降请求队列 MapComponent
// 负责管理地图上所有待处理的升降请求，替代原先的控制台本地队列
public class ULS_LiftRequestMapComponent : MapComponent
{
    // 全局待处理请求队列
    private List<ULS_LiftRequest> globalPendingRequests = new List<ULS_LiftRequest>();

    public ULS_LiftRequestMapComponent(Map map) : base(map)
    {
    }

    // 是否有待处理请求
    public bool HasPendingRequests => globalPendingRequests.Count > 0;

    // 添加升降请求到全局队列
    // 会自动移除针对同一控制器的旧请求（保持每个控制器只有一个请求）
    public void EnqueueRequest(ULS_LiftRequest request)
    {
        if (request == null || request.controller == null)
        {
            return;
        }

        globalPendingRequests ??= new List<ULS_LiftRequest>();

        // 移除针对同一控制器的旧请求（保证每个控制器最多一个待处理请求）
        for (int i = globalPendingRequests.Count - 1; i >= 0; i--)
        {
            if (globalPendingRequests[i].controller == request.controller)
            {
                globalPendingRequests.RemoveAt(i);
            }
        }

        globalPendingRequests.Add(request);
        // 注意：不在这里调用 UpdateLiftDesignation()，因为调用者本身就在 UpdateLiftDesignation() 中，会导致循环调用
    }

    // 取出所有待处理请求并清空队列
    // 返回临时列表（调用者负责使用 SimplePool 管理）
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

    // 清空所有待处理请求（用于模式切换清理）
    public void ClearAllRequests()
    {
        globalPendingRequests?.Clear();
    }

    // 移除针对特定控制器的所有请求
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

        // 注意：不在这里调用 UpdateLiftDesignation()，因为调用者本身可能在 UpdateLiftDesignation() 中
    }

    // 查询特定控制器是否有待处理请求（用于 Designation 判断）
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

    // 获取特定控制器的请求（用于查询）
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

    // 保存/加载
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
