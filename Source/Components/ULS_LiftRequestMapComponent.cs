namespace Universal_Lift_Structure;

// ============================================================
// 【全局升降请求队列管理组件】
// ============================================================
// 此组件负责在地图级别管理所有待处理的升降请求
//
// 【继承关系】
// - 继承自 MapComponent：RimWorld 的地图组件基类，提供序列化和生命周期管理
//
// 【核心职责】
// 1. 请求队列管理：维护全局的升降请求队列（globalPendingRequests）
// 2. 请求去重：确保每个控制器在队列中最多只有一个待处理请求
// 3. 请求查询：提供查询特定控制器的请求状态
// 4. 批量处理：支持批量取出所有待处理请求供工作系统处理
//
// 【设计理念】
// - 替代原先的控制台本地队列设计
// - 集中管理所有升降请求，避免分散存储
// - 简化请求生命周期管理
//
// 【工作模式】
// 1. Console 模式：
//    - WorkGiver 扫描此队列
//    - 发现待处理请求后分派 Job 给 Pawn
// 2. Interactive 模式：
//    - 控制器直接执行升降逻辑
//    - 不依赖此队列
//
// 【请求去重机制】
// - EnqueueRequest 时自动移除同一控制器的旧请求
// - 保证每个控制器最多只有一个待处理请求
// - 避免重复执行和状态冲突
//
// 【数据持久化】
// - 使用 Scribe_Collections.Look 序列化请求队列
// - 支持 LookMode.Deep，完整保存请求数据
// - 加载后自动恢复队列状态
//
// 【使用方式】
// - 通过 map.GetComponent<ULS_LiftRequestMapComponent>() 获取实例
// - 控制器调用 EnqueueRequest() 添加请求
// - WorkGiver 调用 DequeueAllRequests() 批量取出请求
// ============================================================

// 继承自 MapComponent，可持久化的地图组件
public class ULS_LiftRequestMapComponent : MapComponent
{
    // ============================================================
    // 【字段说明】
    // ============================================================
    // globalPendingRequests：全局待处理请求队列
    // - 存储所有待处理的升降请求
    // - 每个控制器最多只有一个待处理请求（自动去重）
    // - 会被序列化保存到存档文件
    // ============================================================

    // 全局待处理请求队列（会被序列化）
    private List<ULS_LiftRequest> globalPendingRequests = new List<ULS_LiftRequest>();

    // ============================================================
    // 【构造函数】
    // ============================================================
    // 创建地图组件实例
    //
    // 【参数说明】
    // - map: 所属地图
    // ============================================================
    public ULS_LiftRequestMapComponent(Map map) : base(map)
    {
    }

    // ============================================================
    // 【查询属性】
    // ============================================================

    /// 是否有待处理请求（用于快速判断队列是否为空）
    public bool HasPendingRequests => globalPendingRequests.Count > 0;

    // ============================================================
    // 【请求管理方法】
    // ============================================================

    // ============================================================
    // 【添加请求到队列】★★★ 核心方法 ★★★
    // ============================================================
    // 将升降请求添加到全局队列，并自动移除该控制器的旧请求
    //
    // 【去重机制】
    // - 遍历现有队列，移除同一控制器的旧请求
    // - 保证每个控制器最多只有一个待处理请求
    // - 避免重复执行和状态冲突
    //
    // 【注意事项】
    // - 不在此处调用 UpdateLiftDesignation()，避免循环调用
    // - 调用者负责在合适时机更新 Designation 状态
    //
    // 【参数说明】
    // - request: 要添加的升降请求
    // ============================================================
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

    // ============================================================
    // 【批量取出所有请求】
    // ============================================================
    // 将所有待处理请求复制到输出列表，并清空内部队列
    //
    // 【使用场景】
    // - WorkGiver 批量取出请求分派给 Pawn
    // - 一次性处理所有待处理请求
    //
    // 【内存管理】
    // - 调用者负责使用 SimplePool 管理 outList
    // - 此方法只进行数据复制，不负责池化管理
    //
    // 【参数说明】
    // - outList: 接收请求的输出列表（调用者提供）
    // ============================================================
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

    // ============================================================
    // 【清空所有请求】
    // ============================================================
    // 清空全局队列，通常用于模式切换时的清理
    //
    // 【使用场景】
    // - 从 Console 模式切换到 Interactive 模式
    // - 需要丢弃所有未处理的请求
    // - 避免残留请求影响新模式的行为
    // ============================================================
    public void ClearAllRequests()
    {
        globalPendingRequests?.Clear();
    }

    // ============================================================
    // 【移除特定控制器的请求】
    // ============================================================
    // 从队列中移除指定控制器的所有待处理请求
    //
    // 【使用场景】
    // - 控制器被移除或销毁
    // - 控制器状态变化，取消待处理请求
    //
    // 【注意事项】
    // - 不在此处调用 UpdateLiftDesignation()，避免循环调用
    // - 调用者可能本身在 UpdateLiftDesignation() 中
    //
    // 【参数说明】
    // - controller: 目标控制器
    // ============================================================
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

    // ============================================================
    // 【查询控制器是否有待处理请求】
    // ============================================================
    // 检查指定控制器是否在队列中有待处理请求
    //
    // 【使用场景】
    // - 更新 Designation 状态时判断是否需要显示标记
    // - UI 显示控制器是否有待处理操作
    //
    // 【参数说明】
    // - controller: 要查询的控制器
    //
    // 【返回值】
    // - true 如果有待处理请求；否则 false
    // ============================================================
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

    // ============================================================
    // 【获取控制器的请求对象】
    // ============================================================
    // 返回指定控制器的待处理请求对象（如果存在）
    //
    // 【使用场景】
    // - 查询请求的详细信息（如升降方向）
    // - 判断请求类型以决定后续逻辑
    //
    // 【参数说明】
    // - controller: 目标控制器
    //
    // 【返回值】
    // - 请求对象，如果不存在则返回 null
    // ============================================================
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

    // ============================================================
    // 【序列化方法】
    // ============================================================

    // ============================================================
    // 【序列化/反序列化】
    // ============================================================
    // 保存和加载请求队列数据
    //
    // 【序列化内容】
    // - globalPendingRequests：使用 LookMode.Deep 完整保存请求数据
    //
    // 【加载后处理】
    // - 检查队列是否为 null，如果是则创建新的空列表
    // - 防御性编程，确保队列始终可用
    // ============================================================
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
