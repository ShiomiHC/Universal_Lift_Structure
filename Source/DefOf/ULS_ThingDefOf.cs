namespace Universal_Lift_Structure;

// ============================================================
// 【ThingDef 引用容器】
// ============================================================
// 此类使用 RimWorld 的 DefOf 系统，提供对 XML 定义的 ThingDef 的静态引用
//
// 【DefOf 系统工作原理】
// 1. 使用 [DefOf] 特性标记类
// 2. RimWorld 在游戏启动时会自动扫描所有 DefOf 类
// 3. 根据字段名称自动从 DefDatabase 中查找匹配的 Def 并赋值
// 4. 如果找不到匹配的 Def，会产生错误日志
//
// 【字段命名规则】
// - 字段名必须与 XML 中定义的 defName 完全一致
// - 例如：public static ThingDef ULS_WallController;
//   对应 XML 中 <ThingDef><defName>ULS_WallController</defName></ThingDef>
//
// 【优势】
// - 编译时类型安全：避免拼写错误和类型错误
// - 性能优化：避免运行时反复查找 Def
// - 代码可读性：清晰标识项目使用的所有 Def
//
// 【静态构造函数】
// - 调用 DefOfHelper.EnsureInitializedInCtor() 确保 DefOf 系统正确初始化
// - 这是防御性编程，确保在使用前所有引用都已填充
// ============================================================
[DefOf]
public static class ULS_ThingDefOf
{
    // ============================================================
    // 【静态构造函数】
    // ============================================================
    // 在类首次被访问时调用，确保 DefOf 系统正确初始化
    // DefOfHelper.EnsureInitializedInCtor() 会触发自动填充流程
    // ============================================================
    static ULS_ThingDefOf()
    {
        DefOfHelper.EnsureInitializedInCtor(typeof(ULS_ThingDefOf));
    }

    // ============================================================
    // 【ThingDef 引用字段】
    // ============================================================
    // 这些字段会在游戏加载时自动填充
    // 对应 Defs/ThingDefs/ 目录下的 XML 定义
    // ============================================================

    // 升降机临时阻挡物
    // 用途：在升降过程中临时占据建筑物的位置，防止其他建筑或小人进入
    // 特性：隐藏建筑，不可选中，仅供内部逻辑使用
    public static ThingDef ULS_LiftBlocker;

    // 升降控制台
    // 用途：在控制台模式下，小人通过此建筑操作升降机
    // 关联组件：CompLiftConsole
    public static ThingDef ULS_LiftConsole;

    // 升降机控制器
    // 用途：项目的核心建筑，管理单个或多个建筑的升降
    // 关联类：Building_WallController
    public static ThingDef ULS_WallController;

    // 自动模式的升降机控制器（已废弃/未实现）
    // 注释说明：此定义可能用于未来的自动化功能
    // public static ThingDef ULS_WallController_Auto;
}
