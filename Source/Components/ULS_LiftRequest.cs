namespace Universal_Lift_Structure;

// ============================================================
// 【升降请求类型枚举】
// ============================================================
// 定义升降请求的两种操作类型
//
// 【用途】
// - 标识升降请求是升起还是降下操作
// - 用于 ULS_LiftRequest 中区分请求类型
// ============================================================
public enum ULS_LiftRequestType
{
    // 升起编组
    // 将编组内所有建筑升起到地表
    RaiseGroup,

    // 降下编组
    // 将编组内所有建筑降下到地下
    LowerGroup
}

// ============================================================
// 【升降请求数据结构】
// ============================================================
// 此类表示一个升降操作请求，包含操作类型、目标控制器和起始位置
//
// 【继承关系】
// - 实现 IExposable：支持序列化/反序列化，可保存到存档
//
// 【核心职责】
// 1. 存储升降请求的所有必要信息
// 2. 支持请求队列的持久化
// 3. 提供无参构造函数以支持 Scribe 反序列化
//
// 【数据字段】
// - type：请求类型（升起/降下）
// - controller：目标控制器的引用
// - startCell：请求发起时控制器的位置（用于验证）
//
// 【使用场景】
// - 玩家点击 Gizmo 按钮触发升降操作
// - 请求被添加到 ULS_LiftRequestMapComponent 的队列中
// - 系统按照队列顺序逐个处理请求
//
// 【序列化注意事项】
// - controller 使用 Scribe_References 保存，支持交叉引用
// - startCell 用于验证控制器是否已移动（如果移动则请求失效）
// ============================================================
public class ULS_LiftRequest : IExposable
{
    // ============================================================
    // 【字段说明】
    // ============================================================
    // 所有字段都会被序列化保存到存档
    // ============================================================

    // 请求类型（升起/降下）
    public ULS_LiftRequestType type;

    // 目标控制器引用
    // 使用 Scribe_References 保存，支持跨 Map 引用
    public Building_WallController controller;

    // 请求发起时的控制器位置
    // 用途：验证控制器是否已被移动或销毁
    // 如果当前位置与此字段不匹配，请求应被视为无效
    public IntVec3 startCell = IntVec3.Invalid;

    // ============================================================
    // 【构造函数】
    // ============================================================

    /// 无参构造函数
    /// 用途：Scribe 反序列化时使用
    /// 注意：此构造函数创建的对象字段值为默认值，需通过 ExposeData() 填充
    public ULS_LiftRequest()
    {
    }

    /// 完整构造函数
    /// 参数：
    ///   - type: 请求类型
    ///   - controller: 目标控制器
    ///   - startCell: 控制器当前位置
    public ULS_LiftRequest(ULS_LiftRequestType type, Building_WallController controller, IntVec3 startCell)
    {
        this.type = type;
        this.controller = controller;
        this.startCell = startCell;
    }

    // ============================================================
    // 【序列化方法】
    // ============================================================
    // 处理保存和加载逻辑
    //
    // 【Scribe 类型说明】
    // - Scribe_Values：用于值类型（枚举、结构体）
    // - Scribe_References：用于引用类型（Thing、Building 等）
    //
    // 【重要】
    // - type 和 startCell 使用 Scribe_Values 保存
    // - controller 使用 Scribe_References 保存，以支持跨对象引用
    // ============================================================
    public void ExposeData()
    {
        // 保存/加载请求类型
        Scribe_Values.Look(ref type, "type");

        // 保存/加载控制器引用
        // 加载时如果控制器已被销毁，controller 会被设为 null
        Scribe_References.Look(ref controller, "controller");

        // 保存/加载起始位置
        // 默认值为 IntVec3.Invalid，表示位置无效
        Scribe_Values.Look(ref startCell, "startCell", IntVec3.Invalid);
    }
}