namespace Universal_Lift_Structure;

// ============================================================
// 【多格建筑分组记录】
// ============================================================
// 此类表示一个多格建筑（如 3x3 大门）的分组信息
//
// 【继承关系】
// - 实现 IExposable：支持序列化/反序列化，可保存到存档
//
// 【核心职责】
// 1. 记录多格建筑的根单元格位置
// 2. 维护主控制器和成员控制器的位置列表
// 3. 支持多格建筑的分组管理和序列化
//
// 【数据结构说明】
// - rootCell：多格建筑的根单元格（通常是左下角）
// - masterControllerCell：主控制器的位置（负责统筹整个多格建筑）
// - memberControllerCells：成员控制器的位置列表（多格建筑的其他单元格）
//
// 【使用场景】
// - 玩家建造多格建筑时，系统创建此记录
// - 记录被存储在 ULS_MultiCellGroupMapComponent 中
// - 升降操作时，系统通过此记录找到所有相关控制器
//
// 【设计原理】
// - 多格建筑的每个单元格都有一个控制器
// - 但只有主控制器负责实际的升降逻辑
// - 成员控制器的 Gizmo 和操作会转发给主控制器
// - 这样可以确保多格建筑作为一个整体升降
// ============================================================
public class ULS_MultiCellGroupRecord : IExposable
{
    // ============================================================
    // 【字段说明】
    // ============================================================
    // 所有字段都会被序列化保存到存档
    // ============================================================

    // 多格建筑的根单元格位置
    // 用途：唯一标识一个多格建筑
    // 通常是多格建筑占据的单元格中坐标最小的那个（左下角）
    public IntVec3 rootCell;

    // 主控制器的单元格位置
    // 用途：指向负责统筹整个多格建筑升降的控制器
    // 特性：主控制器的 Gizmo 按钮会显示完整的操作选项
    public IntVec3 masterControllerCell;

    // 成员控制器的单元格位置列表
    // 用途：记录多格建筑其他单元格的控制器位置
    // 特性：成员控制器的操作会转发给主控制器
    // 注意：此列表不包含主控制器自己
    public List<IntVec3> memberControllerCells;

    // ============================================================
    // 【构造函数】
    // ============================================================

    /// 无参构造函数
    /// 用途：Scribe 反序列化时使用
    /// 注意：必须初始化 memberControllerCells 为空列表，否则会导致空引用异常
    public ULS_MultiCellGroupRecord()
    {
        // 初始化成员列表为空列表
        // 使用 new() 是 C# 9.0 的目标类型语法，等价于 new List<IntVec3>()
        memberControllerCells = new();
    }

    /// 完整构造函数
    /// 参数：
    ///   - rootCell: 多格建筑的根单元格
    ///   - masterControllerCell: 主控制器位置
    ///   - memberControllerCells: 成员控制器位置列表（可为 null）
    public ULS_MultiCellGroupRecord(IntVec3 rootCell, IntVec3 masterControllerCell, List<IntVec3> memberControllerCells)
    {
        this.rootCell = rootCell;
        this.masterControllerCell = masterControllerCell;

        // 防御性编程：如果传入 null，创建空列表
        // ?? 运算符：如果左侧为 null，则使用右侧的值
        this.memberControllerCells = memberControllerCells ?? new();
    }

    // ============================================================
    // 【序列化方法】
    // ============================================================
    // 处理保存和加载逻辑
    //
    // 【字段序列化类型】
    // - rootCell: IntVec3 结构体，使用 Scribe_Values
    // - masterControllerCell: IntVec3 结构体，使用 Scribe_Values
    // - memberControllerCells: List<IntVec3>，使用 Scribe_Collections
    //
    // 【重要】
    // - IntVec3 是值类型（结构体），使用 LookMode.Value
    // - 默认值为 IntVec3.Invalid，表示位置无效
    // ============================================================
    public void ExposeData()
    {
        // 保存/加载根单元格位置
        // 默认值为 IntVec3.Invalid，表示无效位置
        Scribe_Values.Look(ref rootCell, "rootCell", IntVec3.Invalid);

        // 保存/加载主控制器位置
        Scribe_Values.Look(ref masterControllerCell, "masterControllerCell", IntVec3.Invalid);

        // 保存/加载成员控制器位置列表
        // LookMode.Value：因为 IntVec3 是值类型（结构体）
        // 加载后如果为 null，Scribe 会自动创建空列表
        Scribe_Collections.Look(ref memberControllerCells, "memberControllerCells", LookMode.Value);
    }
}